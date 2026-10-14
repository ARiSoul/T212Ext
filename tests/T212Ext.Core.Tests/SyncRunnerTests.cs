using System.Net;
using Arisoul.T212Ext.Client;
using Arisoul.T212Ext.Core.Accounts;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Import;
using Arisoul.T212Ext.Core.Rates;
using Arisoul.T212Ext.Core.Reports;
using Arisoul.T212Ext.Core.Sync;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Tests;

[Collection(DatabaseCollection.Name)]
public class SyncRunnerTests(DatabaseFixture database)
{
    // Far from the other tests' instrument runs, so "stale" depends only on this test.
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2090, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(Random.Shared.Next(1_000_000)));
    private readonly FakeT212ApiFactory _factory = new();

    private SyncRunner CreateRunner(params AccountOptions[] accounts) => CreateRunner(new ReportOptions { Enabled = false }, accounts);

    private SyncRunner CreateRunner(ReportOptions reports, params AccountOptions[] accounts)
    {
        var registry = new AccountRegistry(Options.Create(new T212Options { Accounts = [.. accounts] }), _time);
        var reportOptions = Options.Create(reports);
        return new SyncRunner(
            registry,
            _factory,
            new AccountSynchronizer(database, _time, NullLogger<AccountSynchronizer>.Instance),
            database,
            new ExchangeRateUpdater(database, new FakeRateSource(), _time, NullLogger<ExchangeRateUpdater>.Instance),
            new ReportImporter(database, new CsvImportService(database, _time), reportOptions, _time, NullLogger<ReportImporter>.Instance),
            Options.Create(new SyncOptions { InstrumentsMaxAge = TimeSpan.FromDays(1) }),
            reportOptions,
            _time,
            NullLogger<SyncRunner>.Instance);
    }

    private async Task<(BrokerAccount Account, AccountOptions Options, FakeT212Api Api)> SetUpAccountAsync(bool withKey = true)
    {
        var account = await database.CreateAccountAsync();
        var options = new AccountOptions { Key = account.Key, Name = account.Name, ApiKey = withKey ? "k" : "", ApiSecret = withKey ? "s" : "" };
        var api = new FakeT212Api { Dividends = [FakeT212Api.Dividend("d1", _time.Now, 1)] };
        _factory.Apis[account.Key] = api;
        return (account, options, api);
    }

    private async Task<List<SyncRun>> RunsOfAsync(int accountId)
    {
        await using var db = database.CreateDbContext();
        return await db.SyncRuns.Where(r => r.AccountId == accountId).OrderBy(r => r.Id).ToListAsync();
    }

    [Fact]
    public async Task Every_job_is_logged_and_a_failure_does_not_stop_the_others()
    {
        var (account, options, api) = await SetUpAccountAsync();
        api.FailOrders = new T212ApiException("Trading 212 answered 500", HttpStatusCode.InternalServerError);

        await CreateRunner(options).RunAsync(new SyncRequest(), default);

        var runs = await RunsOfAsync(account.Id);
        Assert.Equal([SyncJob.Snapshot, SyncJob.Dividends, SyncJob.Orders, SyncJob.Transactions], runs.Select(r => r.Job));
        Assert.Equal([true, true, false, true], runs.Select(r => r.Succeeded));
        Assert.Equal("Trading 212 answered 500", runs[2].Error);
        Assert.Equal(1, runs[1].ItemsAdded);
    }

    [Fact]
    public async Task With_reports_on_the_report_job_runs_after_the_history()
    {
        var (account, options, api) = await SetUpAccountAsync();

        await CreateRunner(new ReportOptions { WaitForReport = TimeSpan.FromMinutes(5), PollInterval = TimeSpan.Zero }, options).RunAsync(new SyncRequest(), default);

        var runs = await RunsOfAsync(account.Id);
        Assert.Equal([SyncJob.Snapshot, SyncJob.Dividends, SyncJob.Orders, SyncJob.Transactions, SyncJob.Reports], runs.Select(r => r.Job));
        Assert.True(runs[^1].Succeeded);
        Assert.Single(api.ReportRequests);
    }

    [Fact]
    public async Task A_refused_key_stops_that_account()
    {
        var (account, options, api) = await SetUpAccountAsync();
        api.FailEverything = new T212ApiException("refused", HttpStatusCode.Unauthorized);

        await CreateRunner(options).RunAsync(new SyncRequest(), default);

        var run = Assert.Single(await RunsOfAsync(account.Id));
        Assert.Equal(SyncJob.Snapshot, run.Job);
        Assert.False(run.Succeeded);
    }

    [Fact]
    public async Task Accounts_without_a_key_are_skipped()
    {
        var (account, options, api) = await SetUpAccountAsync(withKey: false);

        Assert.False(await CreateRunner(options).RunAsync(new SyncRequest(), default));

        Assert.Empty(await RunsOfAsync(account.Id));
        Assert.Equal(0, api.SummaryCalls);
    }

    [Fact]
    public async Task A_request_for_one_account_leaves_the_others_alone()
    {
        var (first, firstOptions, _) = await SetUpAccountAsync();
        var (second, secondOptions, secondApi) = await SetUpAccountAsync();

        await CreateRunner(firstOptions, secondOptions).RunAsync(new SyncRequest(AccountKey: first.Key), default);

        Assert.NotEmpty(await RunsOfAsync(first.Id));
        Assert.Empty(await RunsOfAsync(second.Id));
        Assert.Equal(0, secondApi.SummaryCalls);
    }

    [Fact]
    public async Task Instruments_are_refreshed_only_when_stale()
    {
        // Later than any other test's clock, so their instrument runs are older than a day.
        _time.Now = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var (_, options, api) = await SetUpAccountAsync();
        var runner = CreateRunner(options);

        await runner.RunAsync(new SyncRequest(), default);
        Assert.Equal(1, api.InstrumentCalls);

        _time.Now += TimeSpan.FromHours(23);
        await runner.RunAsync(new SyncRequest(), default);
        Assert.Equal(1, api.InstrumentCalls);

        _time.Now += TimeSpan.FromHours(2);
        await runner.RunAsync(new SyncRequest(), default);
        Assert.Equal(2, api.InstrumentCalls);
    }
}
