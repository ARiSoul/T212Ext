using Arisoul.T212Ext.Core.Analytics;

namespace Arisoul.T212Ext.Core.Tests;

public class DividendGrowthTests
{
    private const int ThisYear = 2026;

    private static PerShareFact Paid(int year, int month, decimal perShare, string type = "ORDINARY", string isin = "US7561091049") =>
        new("O_US_EQ", "Realty Income", isin, "USD", new DateOnly(year, month, 15), perShare, type);

    private static IEnumerable<PerShareFact> Quarterly(int year, decimal perShare) =>
        [Paid(year, 3, perShare), Paid(year, 6, perShare), Paid(year, 9, perShare), Paid(year, 12, perShare)];

    private static DividendGrowth Single(IEnumerable<PerShareFact> facts) =>
        Assert.Single(DividendGrowthAnalysis.Compute(facts, ThisYear));

    [Fact]
    public void Growth_streak_and_compound_rate_come_from_comparable_full_years()
    {
        var growth = Single([.. Quarterly(2023, 0.24m), .. Quarterly(2024, 0.25m), .. Quarterly(2025, 0.26m), Paid(2026, 3, 0.27m)]);

        Assert.Equal([2023, 2024, 2025, 2026], growth.Years.Select(y => y.Year));
        Assert.Equal([null, 0.0417m, 0.04m, null], growth.Years.Select(y => y.Growth));
        Assert.True(growth.Years[^1].Partial);   // the current year is never compared
        Assert.Equal(0.04m, growth.LastGrowth);
        Assert.Equal(2, growth.GrowthStreak);
        Assert.Equal((0.0408m, 2), (growth.Cagr, growth.CagrYears));
        Assert.False(growth.Cut);
    }

    [Fact]
    public void Years_with_a_different_number_of_payments_are_not_compared()
    {
        // December 2024's payment slipped into January 2025: 4, 3 and 5 payments.
        var growth = Single([.. Quarterly(2023, 0.25m), Paid(2024, 3, 0.25m), Paid(2024, 6, 0.25m), Paid(2024, 9, 0.25m),
            Paid(2025, 1, 0.25m), .. Quarterly(2025, 0.25m)]);

        Assert.All(growth.Years, y => Assert.Null(y.Growth));
        Assert.Null(growth.LastGrowth);
        Assert.Equal(0, growth.GrowthStreak);
        Assert.Null(growth.Cagr);
    }

    [Fact]
    public void A_lower_dividend_per_share_is_a_cut()
    {
        var growth = Single([.. Quarterly(2024, 0.50m), .. Quarterly(2025, 0.30m)]);

        Assert.Equal(-0.4m, growth.LastGrowth);
        Assert.True(growth.Cut);
        Assert.Equal(0, growth.GrowthStreak);
    }

    [Fact]
    public void A_payment_seen_in_two_accounts_counts_once_and_returns_of_capital_are_not_dividends()
    {
        var growth = Single([.. Quarterly(2024, 0.25m), .. Quarterly(2024, 0.25m), .. Quarterly(2025, 0.26m),
            Paid(2025, 6, 1.00m, "RETURN_OF_CAPITAL_NON_US"), Paid(2025, 7, 1.00m, "Return of capital")]);

        Assert.Equal((1.00m, 4), (growth.Years[0].PerShare, growth.Years[0].Payments));
        Assert.Equal((1.04m, 4), (growth.Years[1].PerShare, growth.Years[1].Payments));
        Assert.Equal(0.04m, growth.LastGrowth);
    }

    [Fact]
    public void A_year_without_payments_breaks_the_comparison()
    {
        var growth = Single([.. Quarterly(2023, 0.25m), .. Quarterly(2025, 0.30m)]);

        Assert.Null(growth.LastGrowth);
        Assert.Null(growth.Cagr);
    }

    [Fact]
    public void Instruments_are_kept_apart_by_isin()
    {
        var all = DividendGrowthAnalysis.Compute([.. Quarterly(2025, 0.25m), Paid(2025, 5, 0.40m, isin: "US0378331005")], ThisYear);

        Assert.Equal(2, all.Count);
    }
}
