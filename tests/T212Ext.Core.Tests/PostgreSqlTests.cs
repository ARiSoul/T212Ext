using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Import;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Tests;

/// <summary>What the move to PostgreSQL (3.0.0) relies on: instants in UTC, codes in upper case, the copy.</summary>
[Collection(DatabaseCollection.Name)]
public class PostgreSqlTests(DatabaseFixture database)
{
    private static readonly TimeSpan Lisbon = TimeSpan.FromHours(1);

    private static Dividend Dividend(int accountId, DateTimeOffset paidOn, string? isin = "US0378331005") => new()
    {
        AccountId = accountId, Reference = Guid.NewGuid().ToString("N"), Sources = DataSources.Api, Ticker = "AAPL_US_EQ", Isin = isin,
        Type = "ORDINARY", PaidOn = paidOn, Quantity = 1, Amount = 1, Currency = "EUR",
    };

    [Fact]
    public async Task An_instant_with_any_offset_is_stored_and_compared_in_utc()
    {
        // timestamptz refuses a DateTimeOffset whose offset is not 0: the model converts it.
        var account = await database.CreateAccountAsync();
        var paidOn = new DateTimeOffset(2030, 3, 10, 9, 30, 0, Lisbon);
        await using (var db = database.CreateDbContext())
        {
            db.Dividends.Add(Dividend(account.Id, paidOn));
            await db.SaveChangesAsync();
        }

        await using var read = database.CreateDbContext();
        var stored = await read.Dividends.SingleAsync(d => d.AccountId == account.Id && d.PaidOn == new DateTimeOffset(2030, 3, 10, 10, 30, 0, TimeSpan.FromHours(2)));
        Assert.Equal(paidOn, stored.PaidOn);
        Assert.Equal(TimeSpan.Zero, stored.PaidOn.Offset);
    }

    [Fact]
    public void Codes_from_a_csv_file_are_stored_in_upper_case()
    {
        const string csv = "Action,Time,ISIN,Ticker,Name,No. of shares,Price / share,Currency (Price / share),Exchange rate,Total,Currency (Total),ID\n"
            + "Dividend (Ordinary),2030-03-10 09:30:00,us0378331005,AAPL,Apple,1,0.25,usd,1.1,0.22,eur,\n";

        var row = Assert.Single(T212CsvParser.Parse(new StringReader(csv)).Rows);

        Assert.Equal(("US0378331005", "USD", "EUR"), (row.Isin, row.PriceCurrency, row.TotalCurrency));
    }

    [Fact]
    public async Task The_copy_moves_every_row_and_the_keys_go_on_from_the_highest()
    {
        // Rows in every kind of table, owned ones included, besides what the other tests left.
        var account = await database.CreateAccountAsync();
        await using (var db = database.CreateDbContext())
        {
            db.Dividends.Add(Dividend(account.Id, new DateTimeOffset(2030, 1, 5, 0, 0, 0, TimeSpan.Zero)));
            db.Snapshots.Add(new PortfolioSnapshot
            {
                AccountId = account.Id, Date = new DateOnly(2030, 1, 5), TakenAt = DateTimeOffset.UtcNow,
                Positions = [new PositionSnapshot { Ticker = "AAPL_US_EQ", Quantity = 3 }],
            });
            db.Orders.Add(new HistoricalOrder
            {
                AccountId = account.Id, OrderId = 1, Ticker = "AAPL_US_EQ", ImportedAt = DateTimeOffset.UtcNow,
                Taxes = [new OrderTax { Name = "STAMP_DUTY", Quantity = 0.5m }],
            });
            await db.SaveChangesAsync();
        }

        var targetOptions = database.OptionsFor($"t212ext_copy_{Guid.NewGuid():N}");
        try
        {
            await using var source = database.CreateDbContext();
            await using (var target = new T212ExtDbContext(targetOptions))
            {
                var counts = await DatabaseCopy.CopyAsync(source, target, log: null);

                Assert.All(counts, c => Assert.True(c.Same, $"{c.Table}: {c.Source} → {c.Target}"));
                Assert.Contains(counts, c => c.Table == "PositionSnapshots" && c.Target > 0);
                Assert.Contains(counts, c => c.Table == "OrderTaxes" && c.Target > 0);
            }

            // A new row after the copy takes the next key, not 1.
            await using (var target = new T212ExtDbContext(targetOptions))
            {
                var highest = await target.Dividends.MaxAsync(d => d.Id);
                var copiedAccount = await target.Accounts.SingleAsync(a => a.Key == account.Key);
                var added = Dividend(copiedAccount.Id, DateTimeOffset.UtcNow);
                target.Dividends.Add(added);
                await target.SaveChangesAsync();
                Assert.True(added.Id > highest);
            }

            // Never into a database that has rows.
            await using (var again = database.CreateDbContext())
            await using (var target = new T212ExtDbContext(targetOptions))
                await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseCopy.CopyAsync(again, target, log: null));
        }
        finally
        {
            await using var target = new T212ExtDbContext(targetOptions);
            await target.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public void Principals_are_copied_before_the_tables_that_point_to_them()
    {
        using var db = database.CreateDbContext();
        var tables = DatabaseCopy.Tables(db.Model).ToList();

        Assert.True(tables.IndexOf("Accounts") < tables.IndexOf("Dividends"));
        Assert.True(tables.IndexOf("Snapshots") < tables.IndexOf("PositionSnapshots"));
        Assert.Equal(tables.Count, tables.Distinct().Count());
    }
}
