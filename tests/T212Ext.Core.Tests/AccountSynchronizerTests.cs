using Arisoul.T212Ext.Client;
using Arisoul.T212Ext.Client.Models;
using Arisoul.T212Ext.Core.Domain;
using HistoricalOrder = Arisoul.T212Ext.Client.Models.HistoricalOrder;
using ApiTax = Arisoul.T212Ext.Client.Models.Tax;
using Arisoul.T212Ext.Core.Sync;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arisoul.T212Ext.Core.Tests;

[Collection(DatabaseCollection.Name)]
public class AccountSynchronizerTests(DatabaseFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _time = new(Now);

    private AccountSynchronizer Synchronizer => new(database, _time, NullLogger<AccountSynchronizer>.Instance);

    private static FakeT212Api ApiWithDividends(int count) => new()
    {
        Dividends = [.. Enumerable.Range(0, count).Select(i => FakeT212Api.Dividend($"d{i}", Now.AddMonths(-i), 1 + i))],
    };

    [Fact]
    public async Task First_dividend_sync_stores_every_page_and_completes_the_history()
    {
        var account = await database.CreateAccountAsync();
        var api = ApiWithDividends(5);

        var result = await Synchronizer.SyncDividendsAsync(account, api, false, default);

        Assert.Equal(new JobResult(5, 0), result);
        Assert.Equal(3, api.PagesRead);
        await using var db = database.CreateDbContext();
        Assert.Equal(5, await db.Dividends.CountAsync(d => d.AccountId == account.Id));
        var state = await db.SyncStates.SingleAsync(s => s.AccountId == account.Id && s.Job == SyncJob.Dividends);
        Assert.True(state.HistoryComplete);
        Assert.Equal(Now, state.LastSuccessAt);
    }

    [Fact]
    public async Task Next_sync_reads_only_the_first_page_when_nothing_is_new()
    {
        var account = await database.CreateAccountAsync();
        await Synchronizer.SyncDividendsAsync(account, ApiWithDividends(5), false, default);

        var api = ApiWithDividends(5);
        var result = await Synchronizer.SyncDividendsAsync(account, api, false, default);

        Assert.Equal(default, result);
        Assert.Equal(1, api.PagesRead);
    }

    [Fact]
    public async Task A_new_dividend_is_added_once_without_duplicating_the_rest()
    {
        var account = await database.CreateAccountAsync();
        await Synchronizer.SyncDividendsAsync(account, ApiWithDividends(5), false, default);

        var api = ApiWithDividends(5);
        api.Dividends.Insert(0, FakeT212Api.Dividend("new", Now.AddDays(1), 9));
        var result = await Synchronizer.SyncDividendsAsync(account, api, false, default);

        Assert.Equal(new JobResult(1, 0), result);
        await using var db = database.CreateDbContext();
        Assert.Equal(6, await db.Dividends.CountAsync(d => d.AccountId == account.Id));
    }

    [Fact]
    public async Task An_interrupted_first_sync_is_completed_by_the_next_one()
    {
        var account = await database.CreateAccountAsync();
        var broken = ApiWithDividends(5);
        broken.FailOnPage = (2, new T212ApiException("network"));

        await Assert.ThrowsAsync<T212ApiException>(() => Synchronizer.SyncDividendsAsync(account, broken, false, default));

        // Page 1 is now known, but the history is not complete: the next sync must not stop there.
        var api = ApiWithDividends(5);
        var result = await Synchronizer.SyncDividendsAsync(account, api, false, default);

        Assert.Equal(new JobResult(3, 0), result);
        Assert.Equal(3, api.PagesRead);
        await using var db = database.CreateDbContext();
        Assert.True((await db.SyncStates.SingleAsync(s => s.AccountId == account.Id && s.Job == SyncJob.Dividends)).HistoryComplete);
    }

    [Fact]
    public async Task Full_history_reads_every_page_even_when_complete()
    {
        var account = await database.CreateAccountAsync();
        await Synchronizer.SyncDividendsAsync(account, ApiWithDividends(5), false, default);

        var api = ApiWithDividends(5);
        await Synchronizer.SyncDividendsAsync(account, api, true, default);

        Assert.Equal(3, api.PagesRead);
    }

    [Fact]
    public async Task Dividends_keep_the_api_values()
    {
        var account = await database.CreateAccountAsync();
        var api = new FakeT212Api { Dividends = [FakeT212Api.Dividend("ref-1", Now, 2.13m, quantity: 10.5m)] };

        await Synchronizer.SyncDividendsAsync(account, api, false, default);

        await using var db = database.CreateDbContext();
        var dividend = await db.Dividends.SingleAsync(d => d.AccountId == account.Id);
        Assert.Equal("ref-1", dividend.Reference);
        Assert.Equal(DataSources.Api, dividend.Sources);
        Assert.Equal("AAPL_US_EQ", dividend.Ticker);
        Assert.Equal("US0378331005", dividend.Isin);
        Assert.Equal(10.5m, dividend.Quantity);
        Assert.Equal(0.25m, dividend.GrossAmountPerShare);
        Assert.Equal("USD", dividend.InstrumentCurrency);
        Assert.Equal(2.13m, dividend.Amount);
        Assert.Equal("EUR", dividend.Currency);
        Assert.Equal(Now, dividend.PaidOn);
    }

    [Fact]
    public async Task The_api_confirming_a_csv_dividend_merges_into_it()
    {
        var account = await database.CreateAccountAsync();
        await using (var db = database.CreateDbContext())
        {
            db.Dividends.Add(new Dividend
            {
                AccountId = account.Id,
                Reference = "csv:abc",
                Sources = DataSources.Csv,
                Ticker = "AAPL",
                Isin = "US0378331005",
                Type = "Ordinary",
                PaidOn = Now.AddHours(-1),
                Quantity = 10,
                Amount = 2.13m,
                Currency = "EUR",
                WithholdingTax = 0.38m,
                WithholdingTaxCurrency = "USD",
            });
            await db.SaveChangesAsync();
        }

        var result = await Synchronizer.SyncDividendsAsync(account, new FakeT212Api { Dividends = [FakeT212Api.Dividend("api-ref", Now, 2.13m)] }, false, default);

        Assert.Equal(new JobResult(0, 1), result);
        await using var check = database.CreateDbContext();
        var dividend = await check.Dividends.SingleAsync(d => d.AccountId == account.Id);
        Assert.Equal("api-ref", dividend.Reference);
        Assert.Equal(DataSources.Api | DataSources.Csv, dividend.Sources);
        Assert.Equal("AAPL_US_EQ", dividend.Ticker);
        Assert.Equal(0.38m, dividend.WithholdingTax);
    }

    [Fact]
    public async Task Orders_are_stored_with_fill_and_taxes_once()
    {
        var account = await database.CreateAccountAsync();
        var api = new FakeT212Api
        {
            Orders =
            [
                new HistoricalOrder
                {
                    Order = new Order { Id = 100, Ticker = "VWCE_EQ", Side = "BUY", Status = "FILLED", Instrument = new InstrumentRef { Isin = "IE00BK5BQT80" } },
                    Fill = new Fill
                    {
                        Id = 200, Price = 120.5m, Quantity = 1.5m, Type = "TRADE",
                        WalletImpact = new FillWalletImpact { Currency = "EUR", NetValue = -180.75m, Taxes = [new ApiTax { Name = "STAMP_DUTY", Quantity = 0.9m, Currency = "EUR" }] },
                    },
                },
                new HistoricalOrder { Order = new Order { Id = 101, Ticker = "VWCE_EQ", Status = "CANCELLED" } },
            ],
        };

        Assert.Equal(new JobResult(2, 0), await Synchronizer.SyncOrdersAsync(account, api, false, default));
        Assert.Equal(default, await Synchronizer.SyncOrdersAsync(account, api, true, default));

        await using var db = database.CreateDbContext();
        var orders = await db.Orders.Where(o => o.AccountId == account.Id).OrderBy(o => o.OrderId).ToListAsync();
        Assert.Equal(2, orders.Count);
        Assert.Equal(200, orders[0].FillId);
        Assert.Equal(-180.75m, orders[0].NetValue);
        Assert.Equal("IE00BK5BQT80", orders[0].Isin);
        Assert.Equal("STAMP_DUTY", Assert.Single(orders[0].Taxes).Name);
        Assert.Null(orders[1].FillId);
    }

    [Fact]
    public async Task Cash_movements_get_normalised_signs()
    {
        var account = await database.CreateAccountAsync();
        var api = new FakeT212Api
        {
            Transactions =
            [
                new HistoryTransactionItem { Reference = "t1", Type = "WITHDRAW", Amount = 500, Currency = "EUR", DateTime = Now },
                new HistoryTransactionItem { Reference = "t2", Type = "DEPOSIT", Amount = 1000, Currency = "EUR", DateTime = Now.AddDays(-1) },
                new HistoryTransactionItem { Reference = "t3", Type = "INTEREST_ON_FREE_CASH", Amount = 0.42m, Currency = "EUR", DateTime = Now.AddDays(-2) },
            ],
        };

        await Synchronizer.SyncTransactionsAsync(account, api, false, default);

        await using var db = database.CreateDbContext();
        var amounts = await db.CashTransactions.Where(t => t.AccountId == account.Id).OrderBy(t => t.Reference).Select(t => t.Amount).ToListAsync();
        Assert.Equal([-500m, 1000m, 0.42m], amounts);
    }

    [Fact]
    public async Task One_snapshot_per_day_the_last_sync_wins()
    {
        var account = await database.CreateAccountAsync(currency: null);
        var api = new FakeT212Api
        {
            Summary = new AccountSummary { Id = 777, Currency = "EUR", TotalValue = 1000 },
            Positions = [Position("AAPL_US_EQ", 2), Position("VWCE_EQ", 3)],
        };

        Assert.Equal(new JobResult(1, 0), await Synchronizer.SyncSnapshotAsync(account, api, default));

        api.Summary = new AccountSummary { Id = 777, Currency = "EUR", TotalValue = 1100 };
        api.Positions = [Position("AAPL_US_EQ", 4)];
        _time.Now = Now.AddHours(5);
        Assert.Equal(new JobResult(0, 1), await Synchronizer.SyncSnapshotAsync(account, api, default));

        _time.Now = Now.AddDays(1);
        Assert.Equal(new JobResult(1, 0), await Synchronizer.SyncSnapshotAsync(account, api, default));

        await using var db = database.CreateDbContext();
        var snapshots = await db.Snapshots.Include(s => s.Positions).Where(s => s.AccountId == account.Id).OrderBy(s => s.Date).ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(1100, snapshots[0].TotalValue);
        Assert.Equal(4, Assert.Single(snapshots[0].Positions).Quantity);
        var stored = await db.Accounts.SingleAsync(a => a.Id == account.Id);
        Assert.Equal(777, stored.ExternalId);
        Assert.Equal("EUR", stored.Currency);
    }

    [Fact]
    public async Task Instruments_are_added_then_updated_only_when_changed()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var api = new FakeT212Api
        {
            Instruments =
            [
                new TradableInstrument { Ticker = $"{prefix}_A", Isin = "US0000000001", Name = "A", CurrencyCode = "USD" },
                new TradableInstrument { Ticker = $"{prefix}_B", Isin = "US0000000002", Name = "B", CurrencyCode = "USD" },
            ],
        };

        Assert.Equal(new JobResult(2, 0), await Synchronizer.SyncInstrumentsAsync(api, default));

        api.Instruments[1] = new TradableInstrument { Ticker = $"{prefix}_B", Isin = "US0000000002", Name = "B renamed", CurrencyCode = "USD" };
        Assert.Equal(new JobResult(0, 1), await Synchronizer.SyncInstrumentsAsync(api, default));

        await using var db = database.CreateDbContext();
        Assert.Equal("B renamed", (await db.Instruments.SingleAsync(i => i.Ticker == $"{prefix}_B")).Name);
    }

    private static Position Position(string ticker, decimal quantity) => new()
    {
        Instrument = new InstrumentRef { Ticker = ticker, Currency = "USD" },
        Quantity = quantity,
        WalletImpact = new PositionWalletImpact { Currency = "EUR", CurrentValue = quantity * 10 },
    };
}
