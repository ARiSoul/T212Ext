using System.Text;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Import;
using Arisoul.T212Ext.Core.Sync;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arisoul.T212Ext.Core.Tests;

[Collection(DatabaseCollection.Name)]
public class CsvImportServiceTests(DatabaseFixture database)
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));

    private CsvImportService Service => new(database, _time);

    private Task<CsvImportResult> ImportAsync(int accountId, string csv) =>
        Service.ImportAsync(accountId, new MemoryStream(Encoding.UTF8.GetBytes(csv)));

    [Fact]
    public async Task Imports_dividends_and_cash_and_counts_the_rest()
    {
        var account = await database.CreateAccountAsync();

        var result = await ImportAsync(account.Id, T212CsvParserTests.NewLayout);

        Assert.Equal(3, result.DividendsAdded);
        Assert.Equal(3, result.TransactionsAdded);
        Assert.Equal(1, result.OrdersSkipped);
        Assert.Equal(1, result.OtherSkipped["Currency conversion"]);
        Assert.Empty(result.Errors);

        await using var db = database.CreateDbContext();
        var ordinary = await db.Dividends.SingleAsync(d => d.AccountId == account.Id && d.Type == "Ordinary");
        Assert.Equal(DataSources.Csv, ordinary.Sources);
        Assert.StartsWith("csv:", ordinary.Reference);
        Assert.Equal(0.51m, ordinary.Amount);
        Assert.Equal("EUR", ordinary.Currency);
        Assert.Equal(0.51m, ordinary.AmountInEuro);
        Assert.Equal(0.09m, ordinary.WithholdingTax);
        Assert.Equal("USD", ordinary.WithholdingTaxCurrency);
        Assert.Equal(0.25m, ordinary.GrossAmountPerShare);

        var cash = await db.CashTransactions.Where(t => t.AccountId == account.Id).OrderBy(t => t.DateTime).ToListAsync();
        Assert.Equal(["DEPOSIT", "INTEREST_ON_FREE_CASH", "WITHDRAW"], cash.Select(t => t.Type));
        Assert.Equal([1000m, 1.23m, -200m], cash.Select(t => t.Amount));
        Assert.Equal("dep-1", cash[0].Reference);
    }

    [Fact]
    public async Task Imports_the_2026_layout_with_dividend_adjustments_as_cash()
    {
        var account = await database.CreateAccountAsync();

        var result = await ImportAsync(account.Id, T212CsvParserTests.Layout2026);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.DividendsAdded);
        Assert.Equal(3, result.TransactionsAdded);
        Assert.Equal(1, result.OrdersSkipped);
        Assert.Empty(result.OtherSkipped);

        await using var db = database.CreateDbContext();
        Assert.DoesNotContain(await db.Dividends.Where(d => d.AccountId == account.Id).Select(d => d.Ticker).ToListAsync(), t => t == "?");
        var adjustment = await db.CashTransactions.SingleAsync(t => t.AccountId == account.Id && t.Type == CashTransactionTypes.DividendAdjustment);
        Assert.Equal(1.27m, adjustment.Amount);
        Assert.Equal("adj-1", adjustment.Reference);
        Assert.Equal(0.03m, (await db.CashTransactions.SingleAsync(t => t.AccountId == account.Id && t.Type == CashTransactionTypes.LendingInterest)).Amount);
    }

    [Fact]
    public async Task Importing_the_same_file_twice_changes_nothing()
    {
        var account = await database.CreateAccountAsync();
        await ImportAsync(account.Id, T212CsvParserTests.NewLayout);

        var second = await ImportAsync(account.Id, T212CsvParserTests.NewLayout);

        Assert.Equal(0, second.DividendsAdded);
        Assert.Equal(3, second.DividendsMatched);
        Assert.Equal(0, second.TransactionsAdded);
        Assert.Equal(3, second.TransactionsMatched);
        await using var db = database.CreateDbContext();
        Assert.Equal(3, await db.Dividends.CountAsync(d => d.AccountId == account.Id));
        Assert.Equal(3, await db.CashTransactions.CountAsync(t => t.AccountId == account.Id));
    }

    [Fact]
    public async Task Two_payments_of_one_instrument_on_one_day_stay_apart()
    {
        var account = await database.CreateAccountAsync();

        await ImportAsync(account.Id, T212CsvParserTests.NewLayout);

        await using var db = database.CreateDbContext();
        var apple = await db.Dividends.Where(d => d.AccountId == account.Id && d.Isin == "US0378331005").OrderBy(d => d.Amount).ToListAsync();
        Assert.Equal(["Return of capital", "Ordinary"], apple.Select(d => d.Type));
    }

    [Fact]
    public async Task A_report_after_the_api_sync_adds_the_withholding_tax_without_duplicating()
    {
        var account = await database.CreateAccountAsync();
        var api = new FakeT212Api
        {
            Dividends =
            [
                FakeT212Api.Dividend("api-ordinary", new DateTimeOffset(2025, 2, 13, 9, 0, 0, TimeSpan.Zero), 0.51m, quantity: 2.5m),
                FakeT212Api.Dividend("api-roc", new DateTimeOffset(2025, 2, 13, 9, 0, 0, TimeSpan.Zero), 0.24m, quantity: 2.5m),
            ],
        };
        await new AccountSynchronizer(database, _time, NullLogger<AccountSynchronizer>.Instance).SyncDividendsAsync(account, api, false, default);

        var result = await ImportAsync(account.Id, T212CsvParserTests.NewLayout);

        Assert.Equal(2, result.DividendsMatched);
        Assert.Equal(1, result.DividendsAdded);
        await using var db = database.CreateDbContext();
        var ordinary = await db.Dividends.SingleAsync(d => d.AccountId == account.Id && d.Reference == "api-ordinary");
        Assert.Equal(DataSources.Api | DataSources.Csv, ordinary.Sources);
        Assert.Equal(0.09m, ordinary.WithholdingTax);
        Assert.Equal("ORDINARY", ordinary.Type);
        Assert.Equal(3, await db.Dividends.CountAsync(d => d.AccountId == account.Id));
    }

    [Fact]
    public async Task Uses_the_trading_212_ticker_found_by_isin()
    {
        var account = await database.CreateAccountAsync();
        var isin = "XS" + Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);
        var ticker = $"T{Guid.NewGuid():N}"[..12] + "_EQ";
        await using (var db = database.CreateDbContext())
        {
            db.Instruments.Add(new Instrument { Ticker = ticker, Isin = isin, Currency = "USD", UpdatedAt = _time.Now });
            await db.SaveChangesAsync();
        }

        await ImportAsync(account.Id, T212CsvParserTests.OldLayout.Replace("US0378331005", isin));

        await using var check = database.CreateDbContext();
        Assert.Equal(ticker, (await check.Dividends.SingleAsync(d => d.AccountId == account.Id)).Ticker);
    }

    [Fact]
    public async Task Rows_without_a_total_are_reported()
    {
        var account = await database.CreateAccountAsync();

        var result = await ImportAsync(account.Id, "Action,Time,ISIN,Total\nDividend (Ordinary),2025-01-01 10:00:00,US0378331005,\n");

        Assert.Equal(0, result.DividendsAdded);
        Assert.Equal(2, Assert.Single(result.Errors).LineNumber);
    }

    [Fact]
    public async Task Synthetic_references_are_stable()
    {
        var row = Assert.Single(T212CsvParser.Parse(new StringReader(T212CsvParserTests.OldLayout)).Rows);

        Assert.Equal(CsvImportService.SyntheticReference(row), CsvImportService.SyntheticReference(row with { Notes = "other" }));
        Assert.NotEqual(CsvImportService.SyntheticReference(row), CsvImportService.SyntheticReference(row with { Total = 0.49m }));
        await Task.CompletedTask;
    }
}
