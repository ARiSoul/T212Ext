using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Import;
using Arisoul.T212Ext.Core.Queries;
using Arisoul.T212Ext.Core.Reports;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Tests;

[Collection(DatabaseCollection.Name)]
public class ReportImporterTests(DatabaseFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _time = new(Now);
    private readonly FakeT212Api _api = new();

    // Waits long enough for the fake, which never makes the clock move, and checks without pausing.
    private ReportImporter Importer(ReportOptions? options = null) => new(
        database,
        new CsvImportService(database, _time),
        Options.Create(options ?? new ReportOptions { WaitForReport = TimeSpan.FromMinutes(5), PollInterval = TimeSpan.Zero }),
        _time,
        NullLogger<ReportImporter>.Instance);

    private static ReportOptions NoWait => new() { WaitForReport = TimeSpan.Zero, PollInterval = TimeSpan.Zero };

    private async Task<BrokerAccount> AccountWithDividendAsync(DateTimeOffset paidOn)
    {
        var account = await database.CreateAccountAsync();
        await using var db = database.CreateDbContext();
        db.Dividends.Add(new Dividend
        {
            AccountId = account.Id, Reference = Guid.NewGuid().ToString("N"), Sources = DataSources.Api, Ticker = "AAPL_US_EQ",
            Isin = "US0378331005", Type = "ORDINARY", PaidOn = paidOn, Quantity = 10, Amount = 2, Currency = "EUR",
        });
        await db.SaveChangesAsync();
        return account;
    }

    private async Task<List<AccountReport>> ReportsOfAsync(int accountId)
    {
        await using var db = database.CreateDbContext();
        return await db.AccountReports.Where(r => r.AccountId == accountId).OrderBy(r => r.From).ToListAsync();
    }

    [Fact]
    public async Task The_first_run_covers_the_history_from_the_first_activity_one_year_per_report()
    {
        var account = await AccountWithDividendAsync(new DateTimeOffset(2024, 3, 15, 14, 0, 0, TimeSpan.Zero));

        await Importer().RunAsync(account, _api, default);

        var reports = await ReportsOfAsync(account.Id);
        Assert.Equal(
        [
            (new DateTimeOffset(2024, 3, 14, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2025, 3, 14, 0, 0, 0, TimeSpan.Zero)),
            (new DateTimeOffset(2025, 3, 11, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 11, 0, 0, 0, TimeSpan.Zero)),
            (new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.Zero), Now),
        ], reports.Select(r => (r.From, r.To)));   // each one starts 3 days (the overlap) before the last one ends
        Assert.All(reports, r => Assert.Equal(AccountReportStatus.Imported, r.Status));
        Assert.Equal(3, _api.ReportRequests.Count);
    }

    [Fact]
    public async Task The_import_page_lists_the_reports_newest_first_with_the_account()
    {
        var account = await AccountWithDividendAsync(new DateTimeOffset(2024, 3, 15, 14, 0, 0, TimeSpan.Zero));
        await Importer().RunAsync(account, _api, default);

        var rows = await new ReportQueries(database).GetReportsAsync(account.Id);

        Assert.Equal(3, rows.Count);
        Assert.Equal(Now, rows[0].To);   // same request time: the last one asked for comes first
        Assert.All(rows, r => Assert.Equal(account.Name, r.AccountName));
    }

    [Fact]
    public async Task A_report_is_imported_like_an_uploaded_file()
    {
        var account = await AccountWithDividendAsync(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        _api.ReportCsv = _ => T212CsvParserTests.Layout2026;

        var result = await Importer().RunAsync(account, _api, default);

        var report = Assert.Single(await ReportsOfAsync(account.Id));
        Assert.Equal(2, report.DividendsAdded);
        Assert.Equal(3, report.TransactionsAdded);
        Assert.Equal(5, result.Added);
        await using var db = database.CreateDbContext();
        var fromReport = await db.Dividends.SingleAsync(d => d.AccountId == account.Id && d.Isin == "US0378331005" && d.Sources == DataSources.Csv);
        Assert.Equal(0.16m, fromReport.WithholdingTax);
    }

    [Fact]
    public async Task A_new_report_is_due_only_after_the_interval_and_overlaps_the_last_one()
    {
        var account = await AccountWithDividendAsync(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        await Importer().RunAsync(account, _api, default);

        _time.Now = Now.AddDays(6);
        await Importer().RunAsync(account, _api, default);
        Assert.Single(_api.ReportRequests);

        _time.Now = Now.AddDays(7);
        await Importer().RunAsync(account, _api, default);

        var last = (await ReportsOfAsync(account.Id))[^1];
        Assert.Equal((Now.AddDays(-3), Now.AddDays(7)), (last.From, last.To));
        Assert.Equal(AccountReportStatus.Imported, last.Status);
    }

    [Fact]
    public async Task A_report_not_ready_in_time_is_collected_by_the_next_run()
    {
        var account = await AccountWithDividendAsync(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        _api.ReportStatus = "Processing";

        await Importer(NoWait).RunAsync(account, _api, default);
        await Importer(NoWait).RunAsync(account, _api, default);

        var report = Assert.Single(await ReportsOfAsync(account.Id));
        Assert.Equal(AccountReportStatus.Requested, report.Status);
        Assert.Single(_api.ReportRequests);   // never a second request while one is waiting

        _api.ReportStatus = "Finished";
        await Importer(NoWait).RunAsync(account, _api, default);

        Assert.Equal(AccountReportStatus.Imported, Assert.Single(await ReportsOfAsync(account.Id)).Status);
    }

    [Fact]
    public async Task A_failed_report_stops_the_run_and_its_period_is_asked_for_again_next_time()
    {
        var account = await AccountWithDividendAsync(new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero));
        _api.ReportStatus = "Failed";

        await Importer().RunAsync(account, _api, default);

        var failed = Assert.Single(await ReportsOfAsync(account.Id));
        Assert.Equal(AccountReportStatus.Failed, failed.Status);
        Assert.Contains("failed", failed.Error);

        _api.ReportStatus = "Finished";
        await Importer().RunAsync(account, _api, default);

        var reports = await ReportsOfAsync(account.Id);
        Assert.Equal(4, reports.Count);
        Assert.Equal(failed.From, reports.Where(r => r.Status == AccountReportStatus.Imported).Min(r => r.From));
    }

    [Fact]
    public async Task A_report_Trading_212_stops_listing_is_given_up_after_a_day()
    {
        var account = await AccountWithDividendAsync(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        _api.ReportStatus = "Queued";
        await Importer(NoWait).RunAsync(account, _api, default);
        _api.ReportsForgotten.Add(_api.ReportRequests.Keys.Single());

        _time.Now = Now.AddHours(23);
        await Importer(NoWait).RunAsync(account, _api, default);
        Assert.Equal(AccountReportStatus.Requested, Assert.Single(await ReportsOfAsync(account.Id)).Status);

        _time.Now = Now.AddHours(25);
        await Importer(NoWait).RunAsync(account, _api, default);

        var report = Assert.Single(await ReportsOfAsync(account.Id));
        Assert.Equal(AccountReportStatus.Failed, report.Status);
        Assert.Contains("no longer lists", report.Error);
    }

    [Fact]
    public async Task A_file_that_is_not_a_report_fails_with_the_reason()
    {
        var account = await AccountWithDividendAsync(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        _api.ReportCsv = _ => "<html>Access denied</html>";

        await Importer().RunAsync(account, _api, default);

        var report = Assert.Single(await ReportsOfAsync(account.Id));
        Assert.Equal(AccountReportStatus.Failed, report.Status);
        Assert.Contains("Action", report.Error);
    }

    [Fact]
    public async Task An_account_without_data_starts_one_span_ago_or_at_the_configured_start()
    {
        var account = await database.CreateAccountAsync();

        var period = await Importer().NextPeriodAsync(account.Id, default);
        Assert.Equal((new DateTimeOffset(2025, 10, 12, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero)), period);

        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        period = await Importer(new ReportOptions { HistoryStart = start }).NextPeriodAsync(account.Id, default);
        Assert.Equal((start.AddDays(-1), Now), period);
    }
}
