using Arisoul.T212Ext.Client;
using Arisoul.T212Ext.Core.Accounts;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Data.Configurations;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Rates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Sync;

public sealed class SyncOptions
{
    public const string Section = "Sync";

    /// <summary>Turns the scheduled sync off; "Sync now" still works.</summary>
    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
    public bool RunOnStartup { get; set; } = true;

    /// <summary>The instrument list is refreshed when the last refresh is older than this.</summary>
    public TimeSpan InstrumentsMaxAge { get; set; } = TimeSpan.FromDays(1);
}

/// <summary>What to sync. Without an account key, every account with credentials.</summary>
public sealed record SyncRequest(string? AccountKey = null, bool FullHistory = false, string Reason = "manual");

/// <summary>
/// Runs every job of every requested account and logs each run in <see cref="SyncRun"/>.
/// A failed job does not stop the others, except a rejected key, which stops that account.
/// </summary>
public sealed class SyncRunner(
    AccountRegistry registry,
    IT212ApiFactory apiFactory,
    AccountSynchronizer synchronizer,
    IDbContextFactory<T212ExtDbContext> dbFactory,
    ExchangeRateUpdater rateUpdater,
    IOptions<SyncOptions> options,
    TimeProvider time,
    ILogger<SyncRunner> logger)
{
    /// <summary>Reports the step in progress, e.g. "invest: Dividends".</summary>
    public event Action<string>? Progress;

    /// <summary>Returns false when no account could be synced (none configured with a key).</summary>
    public async Task<bool> RunAsync(SyncRequest request, CancellationToken cancellationToken)
    {
        var configured = registry.Configured
            .Where(a => request.AccountKey is null || a.Key == request.AccountKey)
            .Where(a =>
            {
                if (a.ToT212Account().HasCredentials)
                    return true;

                logger.LogInformation("Account {Account} has no API key; sync skipped", a.Key);
                return false;
            })
            .ToList();

        if (configured.Count == 0)
            return false;

        List<BrokerAccount> accounts;
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var keys = configured.Select(a => a.Key).ToList();
            accounts = await db.Accounts.Where(a => keys.Contains(a.Key)).ToListAsync(cancellationToken);
        }

        logger.LogInformation("Sync started ({Reason}) for {Count} account(s)", request.Reason, accounts.Count);

        if (await InstrumentsAreStaleAsync(cancellationToken))
        {
            var api = apiFactory.Create(configured[0].ToT212Account());
            await RunJobAsync(null, SyncJob.Instruments, ct => synchronizer.SyncInstrumentsAsync(api, ct), cancellationToken);
        }

        foreach (var account in accounts)
        {
            var credentials = configured.First(a => a.Key == account.Key).ToT212Account();
            if (credentials.IsLegacyKey)
                logger.LogWarning("Account {Account} uses an older API key without a secret. Trading 212 still accepts these, but may stop; generate a key pair when convenient", account.Key);

            var api = apiFactory.Create(credentials);
            var full = request.FullHistory;

            Func<CancellationToken, Task<JobResult>>[] jobs =
            [
                ct => synchronizer.SyncSnapshotAsync(account, api, ct),
                ct => synchronizer.SyncDividendsAsync(account, api, full, ct),
                ct => synchronizer.SyncOrdersAsync(account, api, full, ct),
                ct => synchronizer.SyncTransactionsAsync(account, api, full, ct),
            ];
            SyncJob[] names = [SyncJob.Snapshot, SyncJob.Dividends, SyncJob.Orders, SyncJob.Transactions];

            for (var i = 0; i < jobs.Length; i++)
            {
                Progress?.Invoke($"{account.Key}: {names[i]}");
                var error = await RunJobAsync(account.Id, names[i], jobs[i], cancellationToken);
                if (error is T212ApiException { IsUnauthorized: true })
                {
                    logger.LogWarning("Account {Account}: API key refused; remaining jobs skipped", account.Key);
                    break;
                }
            }
        }

        // After the dividends, so the rates cover every currency they need (the tax report uses them).
        Progress?.Invoke(SyncJob.ExchangeRates.ToString());
        await RunJobAsync(null, SyncJob.ExchangeRates, async ct => new JobResult(await rateUpdater.UpdateAsync(ct), 0), cancellationToken);

        logger.LogInformation("Sync finished ({Reason})", request.Reason);
        return true;
    }

    private async Task<bool> InstrumentsAreStaleAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var last = await db.SyncRuns
            .Where(r => r.Job == SyncJob.Instruments && r.Succeeded)
            .MaxAsync(r => (DateTimeOffset?)r.StartedAt, cancellationToken);
        return last is null || time.GetUtcNow() - last > options.Value.InstrumentsMaxAge;
    }

    /// <summary>Runs one job and logs it; returns the exception when it failed.</summary>
    private async Task<Exception?> RunJobAsync(int? accountId, SyncJob job, Func<CancellationToken, Task<JobResult>> work, CancellationToken cancellationToken)
    {
        var run = new SyncRun { AccountId = accountId, Job = job, StartedAt = time.GetUtcNow() };
        Exception? failure = null;

        try
        {
            var result = await work(cancellationToken);
            run.Succeeded = true;
            run.ItemsAdded = result.Added;
            run.ItemsUpdated = result.Updated;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            failure = ex;
            run.Error = ex.Message.Length > Lengths.Error ? ex.Message[..Lengths.Error] : ex.Message;
            logger.LogError(ex, "Sync job {Job} failed for account {AccountId}", job, accountId);
        }

        run.FinishedAt = time.GetUtcNow();
        await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
        db.SyncRuns.Add(run);
        await db.SaveChangesAsync(CancellationToken.None);

        return failure;
    }
}
