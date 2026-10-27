using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Import;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Tests;

/// <summary>What PostgreSQL asks of the model (3.0.0): instants in UTC, codes in upper case.</summary>
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
}
