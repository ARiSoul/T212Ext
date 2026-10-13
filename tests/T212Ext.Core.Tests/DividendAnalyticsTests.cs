using Arisoul.T212Ext.Core.Analytics;
using Arisoul.T212Ext.Core.Domain;

namespace Arisoul.T212Ext.Core.Tests;

public class DividendAnalyticsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static DividendFact Div(int year, int month, decimal amount, string isin = "US0378331005", decimal quantity = 10, string ticker = "AAPL_US_EQ", string currency = "USD") =>
        new(new DateTimeOffset(year, month, 15, 10, 0, 0, TimeSpan.Zero), amount, quantity, ticker, "Name " + ticker, isin, currency);

    private static DividendDashboard Compute(IReadOnlyList<DividendFact> dividends, IReadOnlyList<IncomeFact>? income = null, IReadOnlyList<PositionFact>? positions = null) =>
        DividendAnalytics.Compute("EUR", dividends, income ?? [], positions ?? [], [], Now);

    [Fact]
    public void Last_12_months_and_year_to_date_compare_with_the_period_before()
    {
        var result = Compute(
        [
            Div(2026, 9, 10), Div(2026, 2, 5),   // this year
            Div(2025, 11, 4),                    // last 12 months, last year
            Div(2025, 9, 6), Div(2025, 3, 2),    // previous 12 months; 2025-09-15 is also "same period last year"
            Div(2024, 1, 100),                   // older
        ]);

        Assert.Equal(19m, result.Last12Months);
        Assert.Equal(8m, result.Previous12Months);
        Assert.Equal(137.5m, result.Last12MonthsGrowthPercent);
        Assert.Equal(15m, result.YearToDate);
        Assert.Equal(8m, result.SamePeriodLastYear);
        Assert.Equal(87.5m, result.YearToDateGrowthPercent);
        Assert.Equal(127m, result.AllTime);
        Assert.Equal(6, result.PaymentCount);
    }

    [Fact]
    public void Months_are_contiguous_with_a_12_month_moving_average()
    {
        var months = Compute([Div(2025, 1, 12), Div(2025, 6, 24), Div(2026, 1, 36)]).Months;

        Assert.Equal(22, months.Count);  // 2025-01 … 2026-10
        Assert.Equal((2025, 1), (months[0].Year, months[0].Month));
        Assert.Equal((2026, 10), (months[^1].Year, months[^1].Month));
        Assert.Equal(0m, months[1].Amount);
        Assert.Null(months[10].MovingAverage12);
        Assert.Equal(3m, months[11].MovingAverage12);   // 2025-12: (12 + 24) / 12
        Assert.Equal(5m, months[12].MovingAverage12);   // 2026-01: (24 + 36) / 12, January 2025 left the window
    }

    [Fact]
    public void Years_carry_growth_only_against_the_year_before()
    {
        var years = Compute([Div(2023, 5, 10), Div(2024, 5, 15), Div(2026, 5, 30)]).Years;

        Assert.Equal([2023, 2024, 2026], years.Select(y => y.Year));
        Assert.Null(years[0].GrowthPercent);
        Assert.Equal(50m, years[1].GrowthPercent);
        Assert.Null(years[2].GrowthPercent);   // 2025 had nothing: no growth against 2024 is claimed
        Assert.True(years[2].IsCurrentYear);
    }

    [Fact]
    public void Instruments_are_ranked_by_total_with_their_share()
    {
        var result = Compute(
            [Div(2026, 1, 30), Div(2026, 4, 30), Div(2026, 2, 40, isin: "IE00BK5BQT80", ticker: "VWCE_EQ", currency: "EUR")],
            positions: [new PositionFact("VWCE_EQ", "Vanguard", "IE00BK5BQT80", "EUR", 3, 400, 300, 100)]);

        Assert.Equal(["AAPL_US_EQ", "VWCE_EQ"], result.Instruments.Select(i => i.Ticker));
        Assert.Equal(0.6m, result.Instruments[0].ShareOfTotal);
        Assert.Equal(2, result.Instruments[0].Count);
        Assert.False(result.Instruments[0].Held);
        Assert.True(result.Instruments[1].Held);
        Assert.Equal("IE", result.Instruments[1].Country);
    }

    [Fact]
    public void Countries_fold_into_other_past_six()
    {
        var isins = new[] { "US1", "IE1", "GB1", "DE1", "FR1", "NL1", "CH1" };
        var dividends = isins.Select((isin, i) => Div(2026, 1, 70 - i * 10, isin: isin, ticker: isin)).ToList();

        var countries = Compute(dividends).Countries;

        Assert.Equal(["US", "IE", "GB", "DE", "FR", "*"], countries.Select(c => c.Key));
        Assert.Equal(30m, countries[^1].Amount);   // 20 + 10
        Assert.Equal(1m, countries.Sum(c => c.Share));
    }

    [Fact]
    public void Yield_on_cost_and_forward_estimate_use_the_shares_held_today()
    {
        var result = Compute(
            [Div(2026, 2, 2, quantity: 10), Div(2026, 5, 3, quantity: 10), Div(2025, 1, 50)],
            positions: [new PositionFact("AAPL_US_EQ", "Apple", "US0378331005", "USD", 20, 500, 250, 250)]);

        var position = Assert.Single(result.Positions);
        Assert.Equal(5m, position.Last12Months);
        Assert.Equal(0.02m, position.YieldOnCost);
        Assert.Equal(0.01m, position.CurrentYield);
        Assert.Equal(10m, position.ForwardEstimate);   // 0.5 per share over 12 months × 20 shares
        Assert.Equal(10m, result.ForwardEstimate);
        Assert.Equal(0.02m, result.PortfolioYieldOnCost);
    }

    [Fact]
    public void Passive_income_covers_the_last_12_months_by_kind()
    {
        var result = Compute(
            [Div(2026, 10, 5), Div(2025, 9, 99)],
            income:
            [
                new IncomeFact(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), 0.5m, CashTransactionTypes.LendingInterest),
                new IncomeFact(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero), 1.5m, CashTransactionTypes.InterestOnFreeCash),
                new IncomeFact(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero), 2m, CashTransactionTypes.DividendAdjustment),
            ]);

        Assert.Equal(12, result.PassiveIncome.Count);
        Assert.Equal((2025, 11), (result.PassiveIncome[0].Year, result.PassiveIncome[0].Month));
        var october = result.PassiveIncome[^1];
        Assert.Equal(7m, october.Dividends);   // 5 + the tax refund
        Assert.Equal(1.5m, october.Interest);
        Assert.Equal(0.5m, october.Lending);
    }

    [Fact]
    public void No_dividends_gives_an_empty_dashboard()
    {
        var result = Compute([]);

        Assert.Equal(0m, result.AllTime);
        Assert.Empty(result.Months);
        Assert.Empty(result.Countries);
        Assert.Null(result.Last12MonthsGrowthPercent);
    }

    [Theory]
    [InlineData("US0378331005", "US")]
    [InlineData("ie00bk5bqt80", null)]
    [InlineData("X", null)]
    [InlineData(null, null)]
    public void Country_comes_from_the_isin_prefix(string? isin, string? expected) =>
        Assert.Equal(expected, DividendAnalytics.CountryOf(isin));
}
