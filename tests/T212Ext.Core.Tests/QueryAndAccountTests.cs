using Arisoul.T212Ext.Client;
using Arisoul.T212Ext.Core.Accounts;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Queries;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Tests;

[Collection(DatabaseCollection.Name)]
public class QueryAndAccountTests(DatabaseFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Summaries_add_up_per_currency_this_year_and_the_last_12_months()
    {
        var account = await database.CreateAccountAsync();
        await using (var db = database.CreateDbContext())
        {
            db.Dividends.AddRange(
                NewDividend(account.Id, Now.AddDays(-10), 5, "EUR"),
                NewDividend(account.Id, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), 3, "EUR"),
                NewDividend(account.Id, new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero), 2, "EUR"),
                NewDividend(account.Id, new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero), 7, "EUR"),
                NewDividend(account.Id, Now.AddDays(-1), 4, "GBP"));
            await db.SaveChangesAsync();
        }

        var summaries = await new DividendQueries(database, new ManualTimeProvider(Now)).GetSummariesAsync(account.Id);

        Assert.Equal(["EUR", "GBP"], summaries.Select(s => s.Currency));
        var eur = summaries[0];
        Assert.Equal(17m, eur.AllTime);
        Assert.Equal(8m, eur.YearToDate);
        Assert.Equal(10m, eur.Last12Months);
        Assert.Equal(4, eur.Count);
        Assert.Equal([2024, 2025, 2026], eur.ByYear.Select(y => y.Year));
        Assert.Equal([7m, 2m, 8m], eur.ByYear.Select(y => y.Total));
        Assert.Equal(4, eur.ByMonth.Count);
    }

    [Fact]
    public async Task Dividends_filter_by_year_newest_first()
    {
        var account = await database.CreateAccountAsync();
        await using (var db = database.CreateDbContext())
        {
            db.Dividends.AddRange(
                NewDividend(account.Id, new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero), 1, "EUR"),
                NewDividend(account.Id, new DateTimeOffset(2025, 9, 1, 0, 0, 0, TimeSpan.Zero), 2, "EUR"),
                NewDividend(account.Id, new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero), 3, "EUR"));
            await db.SaveChangesAsync();
        }

        var queries = new DividendQueries(database, new ManualTimeProvider(Now));
        var rows = await queries.GetDividendsAsync(account.Id, 2025);

        Assert.Equal([2m, 1m], rows.Select(r => r.Amount));
        Assert.Equal([2025, 2024], await queries.GetYearsAsync(account.Id));
    }

    [Fact]
    public async Task Configured_accounts_get_a_row_and_keep_it()
    {
        var key = $"acc-{Guid.NewGuid():N}"[..20];
        var registry = Registry(new AccountOptions { Key = key, Name = "Invest", Environment = T212Environment.Demo });

        await using (var db = database.CreateDbContext())
        {
            await registry.EnsureAccountsAsync(db);
            await registry.EnsureAccountsAsync(db);
        }

        var renamed = Registry(new AccountOptions { Key = key, Name = "Invest (renamed)" });
        await using (var db = database.CreateDbContext())
            await renamed.EnsureAccountsAsync(db);

        await using var check = database.CreateDbContext();
        var account = await check.Accounts.SingleAsync(a => a.Key == key);
        Assert.Equal("Invest (renamed)", account.Name);
        Assert.Equal("Live", account.Environment);
    }

    [Fact]
    public void Duplicate_or_invalid_account_keys_are_refused()
    {
        var duplicate = Assert.Throws<OptionsValidationException>(() => Registry(
            new AccountOptions { Key = "invest", Name = "A" },
            new AccountOptions { Key = "invest", Name = "B" }));
        Assert.Contains("more than once", duplicate.Message);

        Assert.Throws<OptionsValidationException>(() => Registry(new AccountOptions { Key = "Not Valid!", Name = "A" }));
        Assert.Throws<OptionsValidationException>(() => Registry(new AccountOptions { Key = "invest", Name = "" }));
    }

    [Theory]
    [InlineData("2026-01-01T00:00:00Z", 10, 2.0, true)]
    [InlineData("2026-01-03T23:00:00Z", 10, 2.0, true)]
    [InlineData("2026-01-05T00:00:00Z", 10, 2.0, false)]
    [InlineData("2026-01-01T00:00:00Z", 11, 2.0, false)]
    [InlineData("2026-01-01T00:00:00Z", 10, 2.5, false)]
    public void Dividend_matching_tolerates_time_zones_but_not_other_payments(string paidOn, decimal quantity, double amount, bool matches)
    {
        var stored = NewDividend(1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 2, "EUR");
        stored.Isin = "US0378331005";
        stored.Quantity = 10;

        var match = DividendMatcher.FindMatch([stored], "us0378331005", null, DateTimeOffset.Parse(paidOn), quantity, (decimal)amount);

        Assert.Equal(matches, match is not null);
    }

    private AccountRegistry Registry(params AccountOptions[] accounts) =>
        new(Options.Create(new T212Options { Accounts = [.. accounts] }), new ManualTimeProvider(Now));

    private static Dividend NewDividend(int accountId, DateTimeOffset paidOn, decimal amount, string currency) => new()
    {
        AccountId = accountId,
        Reference = Guid.NewGuid().ToString("N"),
        Sources = DataSources.Api,
        Ticker = "X_EQ",
        Type = "ORDINARY",
        PaidOn = paidOn,
        Quantity = 1,
        Amount = amount,
        Currency = currency,
    };
}
