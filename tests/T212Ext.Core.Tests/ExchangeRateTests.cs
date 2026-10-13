using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Rates;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arisoul.T212Ext.Core.Tests;

public class RateTableTests
{
    private static readonly RateTable Table = new(
    [
        ("USD", new DateOnly(2025, 3, 14), 1.0880m),   // Friday
        ("USD", new DateOnly(2025, 3, 17), 1.0910m),   // Monday
        ("GBP", new DateOnly(2025, 3, 14), 0.8390m),
    ]);

    [Fact]
    public void A_business_day_uses_its_own_rate()
    {
        Assert.Equal((new DateOnly(2025, 3, 17), 1.0910m), Table.RateFor("USD", new DateOnly(2025, 3, 17)));
    }

    [Fact]
    public void A_weekend_uses_the_last_rate_before_it()
    {
        Assert.Equal((new DateOnly(2025, 3, 14), 1.0880m), Table.RateFor("usd", new DateOnly(2025, 3, 16)));
    }

    [Fact]
    public void Without_a_rate_close_enough_there_is_no_conversion()
    {
        Assert.Null(Table.RateFor("USD", new DateOnly(2025, 3, 13)));   // before the first rate
        Assert.Null(Table.RateFor("GBP", new DateOnly(2025, 3, 22)));   // 8 days after the last one
        Assert.Null(Table.RateFor("CHF", new DateOnly(2025, 3, 14)));   // no rates at all
        Assert.Null(Table.ToEuro(10m, null, new DateOnly(2025, 3, 14)));
    }

    [Fact]
    public void Amounts_are_converted_and_rounded_to_cents()
    {
        Assert.Equal(9.19m, Table.ToEuro(10m, "USD", new DateOnly(2025, 3, 14)));   // 10 / 1.088 = 9.1912
        Assert.Equal(10m, Table.ToEuro(10m, "EUR", new DateOnly(2025, 3, 16)));
    }
}

public class EcbRateSourceTests
{
    [Fact]
    public void Reads_the_date_and_value_columns_by_name()
    {
        const string csv = """
            KEY,FREQ,CURRENCY,CURRENCY_DENOM,EXR_TYPE,EXR_SUFFIX,TIME_PERIOD,OBS_VALUE,OBS_STATUS
            EXR.D.USD.EUR.SP00.A,D,USD,EUR,SP00,A,2025-03-14,1.0880,A
            EXR.D.USD.EUR.SP00.A,D,USD,EUR,SP00,A,2025-03-17,1.0910,A
            EXR.D.USD.EUR.SP00.A,D,USD,EUR,SP00,A,2025-03-18,NaN,A
            """;

        var rates = EcbRateSource.Parse(csv);

        Assert.Equal([(new DateOnly(2025, 3, 14), 1.0880m), (new DateOnly(2025, 3, 17), 1.0910m)], rates);
    }

    [Fact]
    public void An_answer_without_the_columns_is_refused() =>
        Assert.Throws<FormatException>(() => EcbRateSource.Parse("A,B\n1,2\n"));
}

[Collection(DatabaseCollection.Name)]
public class ExchangeRateUpdaterTests(DatabaseFixture database)
{
    [Fact]
    public async Task Fetches_the_currencies_dividends_need_from_a_week_before_the_first_then_only_what_is_new()
    {
        // Currencies no other test uses, so the shared database does not get in the way.
        var account = await database.CreateAccountAsync();
        await using (var db = database.CreateDbContext())
        {
            db.Dividends.Add(new Dividend
            {
                AccountId = account.Id, Reference = Guid.NewGuid().ToString("N"), Sources = DataSources.Csv, Ticker = "X",
                Type = "Dividend", PaidOn = new DateTimeOffset(2020, 1, 15, 0, 0, 0, TimeSpan.Zero), Quantity = 1, Amount = 1,
                Currency = "EUR", InstrumentCurrency = "ZAR", WithholdingTaxCurrency = "ZAR",
            });
            await db.SaveChangesAsync();
        }

        var source = new FakeRateSource();
        var time = new ManualTimeProvider(new DateTimeOffset(2020, 1, 31, 12, 0, 0, TimeSpan.Zero));
        var updater = new ExchangeRateUpdater(database, source, time, NullLogger<ExchangeRateUpdater>.Instance);

        Assert.True(await updater.UpdateAsync(default) > 0);
        Assert.Contains(("ZAR", new DateOnly(2020, 1, 8), new DateOnly(2020, 1, 31)), source.Calls);

        time.Now = time.Now.AddDays(5);
        source.Calls.Clear();
        await updater.UpdateAsync(default);

        Assert.Contains(("ZAR", new DateOnly(2020, 2, 1), new DateOnly(2020, 2, 5)), source.Calls);
        await using var check = database.CreateDbContext();
        var dates = await check.ExchangeRates.Where(r => r.Currency == "ZAR").Select(r => r.Date).ToListAsync();
        Assert.Equal(dates.Count, dates.Distinct().Count());
        Assert.DoesNotContain(dates, d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }
}
