using Arisoul.T212Ext.Client;
using Arisoul.T212Ext.Client.Models;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Data.Configurations;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Import;
using Arisoul.T212Ext.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Reports;

public sealed class ReportOptions
{
    public const string Section = "Reports";

    /// <summary>Asks Trading 212 for CSV reports during the sync and imports them.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A new report is asked for once the covered history is older than this.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Each report starts this long before the end of the last one: the latest days are read twice.</summary>
    public TimeSpan Overlap { get; set; } = TimeSpan.FromDays(3);

    /// <summary>The longest period of one report (the app allows one year).</summary>
    public TimeSpan MaxSpan { get; set; } = TimeSpan.FromDays(365);

    /// <summary>How long a sync waits for a report; one not ready by then is collected by the next sync.</summary>
    public TimeSpan WaitForReport { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Time between two status checks (the API allows one a minute).</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Where the first report starts for an account with no data at all; default one <see cref="MaxSpan"/> ago.</summary>
    public DateTimeOffset? HistoryStart { get; set; }
}

/// <summary>
/// Keeps an account's history covered by CSV reports, which carry what the API lacks (the tax
/// withheld on each dividend) and fill its history gaps. Each run collects the reports asked for
/// earlier, then asks for the next period while there is one due, from the account's first
/// activity onwards in periods of at most <see cref="ReportOptions.MaxSpan"/>, and imports them
/// with <see cref="CsvImportService"/> (idempotent, so the overlaps change nothing).
/// </summary>
public sealed class ReportImporter(
    IDbContextFactory<T212ExtDbContext> dbFactory,
    CsvImportService importer,
    IOptions<ReportOptions> options,
    TimeProvider time,
    ILogger<ReportImporter> logger)
{
    /// <summary>A report Trading 212 no longer lists after this long is given up.</summary>
    internal static readonly TimeSpan Forgotten = TimeSpan.FromDays(1);

    // A safety net against a loop: no run asks for more reports than this.
    private const int MaxRequestsPerRun = 10;

    public async Task<JobResult> RunAsync(BrokerAccount account, IT212Api api, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var deadline = time.GetUtcNow() + settings.WaitForReport;
        var total = new JobResult();

        for (var requests = 0; ; requests++)
        {
            var (result, waiting, failed) = await CollectAsync(account, api, deadline, cancellationToken);
            total += result;
            if (waiting || failed || requests >= MaxRequestsPerRun)
                break;

            if (await NextPeriodAsync(account.Id, cancellationToken) is not { } period)
                break;

            await RequestAsync(account, api, period.From, period.To, cancellationToken);
            if (time.GetUtcNow() >= deadline)
                break;
        }

        return total;
    }

    /// <summary>
    /// The period the next report should cover, or null when the covered history is recent enough.
    /// </summary>
    internal async Task<(DateTimeOffset From, DateTimeOffset To)?> NextPeriodAsync(int accountId, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = time.GetUtcNow();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var coveredTo = await db.AccountReports
            .Where(r => r.AccountId == accountId && r.Status == AccountReportStatus.Imported)
            .MaxAsync(r => (DateTimeOffset?)r.To, cancellationToken);

        DateTimeOffset from;
        if (coveredTo is { } end)
        {
            if (now - end < settings.Interval)
                return null;
            from = end - settings.Overlap;
        }
        else
        {
            var first = await FirstActivityAsync(db, accountId, cancellationToken) ?? settings.HistoryStart ?? now - settings.MaxSpan;
            // From the start of the day before, so the first day is whole whatever its time zone.
            from = new DateTimeOffset(first.UtcDateTime.Date, TimeSpan.Zero).AddDays(-1);
        }

        var to = from + settings.MaxSpan < now ? from + settings.MaxSpan : now;
        return (from, to);
    }

    private static async Task<DateTimeOffset?> FirstActivityAsync(T212ExtDbContext db, int accountId, CancellationToken cancellationToken)
    {
        var dividend = await db.Dividends.Where(d => d.AccountId == accountId).MinAsync(d => (DateTimeOffset?)d.PaidOn, cancellationToken);
        var order = await db.Orders.Where(o => o.AccountId == accountId).MinAsync(o => o.FilledAt ?? o.CreatedAt, cancellationToken);
        var cash = await db.CashTransactions.Where(c => c.AccountId == accountId).MinAsync(c => (DateTimeOffset?)c.DateTime, cancellationToken);

        return new[] { dividend, order, cash }.Min();
    }

    private async Task RequestAsync(BrokerAccount account, IT212Api api, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var reportId = await api.RequestReportAsync(new ReportRequest { TimeFrom = from, TimeTo = to }, cancellationToken);
        logger.LogInformation("Account {Account}: asked for report {ReportId} ({From:yyyy-MM-dd} to {To:yyyy-MM-dd})", account.Key, reportId, from, to);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        db.AccountReports.Add(new AccountReport
        {
            AccountId = account.Id,
            ReportId = reportId,
            From = from,
            To = to,
            Status = AccountReportStatus.Requested,
            RequestedAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Imports the account's reports that are ready, checking again every poll interval until none
    /// is left waiting or the deadline passes.
    /// </summary>
    private async Task<(JobResult Result, bool Waiting, bool Failed)> CollectAsync(BrokerAccount account, IT212Api api, DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var total = new JobResult();
        var failed = false;

        while (true)
        {
            List<AccountReport> pending;
            await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
            {
                pending = await db.AccountReports
                    .Where(r => r.AccountId == account.Id && r.Status == AccountReportStatus.Requested)
                    .OrderBy(r => r.From)
                    .ToListAsync(cancellationToken);
            }

            if (pending.Count == 0)
                return (total, false, failed);

            var statuses = (await api.GetReportsAsync(cancellationToken))
                .Where(s => s.ReportId is not null)
                .GroupBy(s => s.ReportId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var report in pending)
            {
                var status = statuses.GetValueOrDefault(report.ReportId);
                switch (status?.Status)
                {
                    case "Finished" when Uri.TryCreate(status.DownloadLink, UriKind.Absolute, out var link):
                        var imported = await ImportAsync(account, api, report, link, cancellationToken);
                        total += imported ?? default;
                        failed |= imported is null;
                        break;

                    case "Failed" or "Canceled":
                        await FinishAsync(report, AccountReportStatus.Failed, $"Trading 212 says the report {status.Status!.ToLowerInvariant()}.", null);
                        failed = true;
                        break;

                    case null when time.GetUtcNow() - report.RequestedAt > Forgotten:
                        await FinishAsync(report, AccountReportStatus.Failed, "Trading 212 no longer lists the report.", null);
                        failed = true;
                        break;
                }
            }

            if (time.GetUtcNow() + settings.PollInterval >= deadline)
            {
                var stillWaiting = pending.Any(r => r.Status == AccountReportStatus.Requested);
                return (total, stillWaiting, failed);
            }

            if (settings.PollInterval > TimeSpan.Zero)
                await Task.Delay(settings.PollInterval, time, cancellationToken);
        }
    }

    /// <summary>Downloads and imports a finished report; null when that failed.</summary>
    private async Task<JobResult?> ImportAsync(BrokerAccount account, IT212Api api, AccountReport report, Uri link, CancellationToken cancellationToken)
    {
        try
        {
            await using var csv = await api.DownloadReportAsync(link, cancellationToken);
            var result = await importer.ImportAsync(account.Id, csv, cancellationToken);
            await FinishAsync(report, AccountReportStatus.Imported, result.Errors.Count > 0 ? $"{result.Errors.Count} line(s) could not be read." : null, result);

            logger.LogInformation(
                "Account {Account}: imported report {ReportId}: {DividendsAdded} new dividends, {DividendsMatched} completed, {TransactionsAdded} new cash movements",
                account.Key, report.ReportId, result.DividendsAdded, result.DividendsMatched, result.TransactionsAdded);
            return new JobResult(result.DividendsAdded + result.TransactionsAdded, result.DividendsMatched + result.TransactionsMatched);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Account {Account}: report {ReportId} could not be imported", account.Key, report.ReportId);
            await FinishAsync(report, AccountReportStatus.Failed, ex.Message, null);
            return null;
        }
    }

    private async Task FinishAsync(AccountReport report, AccountReportStatus status, string? error, CsvImportResult? result)
    {
        report.Status = status;
        report.FinishedAt = time.GetUtcNow();
        report.Error = error is { Length: > Lengths.Error } ? error[..Lengths.Error] : error;
        if (result is not null)
        {
            report.DividendsAdded = result.DividendsAdded;
            report.DividendsMatched = result.DividendsMatched;
            report.TransactionsAdded = result.TransactionsAdded;
            report.TransactionsMatched = result.TransactionsMatched;
        }

        // Saved even when the sync is being stopped, so the outcome is not lost.
        await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
        db.AccountReports.Update(report);
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
