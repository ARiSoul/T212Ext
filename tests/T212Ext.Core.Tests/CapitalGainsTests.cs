using Arisoul.T212Ext.Core.Rates;
using Arisoul.T212Ext.Core.Tax;

namespace Arisoul.T212Ext.Core.Tests;

public class CapitalGainsTests
{
    private const string Isin = "US0378331005";

    private static readonly RateTable Rates = new(
    [
        ("USD", new DateOnly(2024, 1, 10), 1.25m),
        ("USD", new DateOnly(2024, 6, 10), 1.00m),
        ("USD", new DateOnly(2025, 3, 10), 1.25m),
        ("USD", new DateOnly(2025, 3, 12), 1.25m),
        ("USD", new DateOnly(2025, 9, 10), 1.00m),
    ]);

    private static long _id;

    private static TradeInput Trade(TradeSide side, int year, int month, int day, decimal quantity, decimal price, decimal fees = 0, string currency = "USD", string isin = Isin) =>
        new(++_id, isin, "AAPL_US_EQ", "Apple", "STOCK", side, new DateTimeOffset(year, month, day, 15, 0, 0, TimeSpan.Zero), quantity, price, currency, fees, null);

    private static CapitalGainsResult Build(int year, params TradeInput[] trades) =>
        CapitalGains.Build(year, trades, new Dictionary<string, string>(), Rates, type => type == "ETF" ? "G20" : "G01");

    [Fact]
    public void A_sale_is_matched_to_its_purchase_with_each_date_s_rate()
    {
        // Bought 10 × 100 USD at 1.25 = 800 EUR; sold 10 × 150 USD at 1.00 = 1500 EUR.
        var result = Build(2025,
            Trade(TradeSide.Buy, 2024, 1, 10, 10, 100m, fees: 2m),
            Trade(TradeSide.Sell, 2025, 9, 10, 10, 150m, fees: 3m));

        var line = Assert.Single(result.Lines);
        Assert.Equal(1500m, line.RealisationEur);
        Assert.Equal(800m, line.AcquisitionEur);
        Assert.Equal(5m, line.ExpensesEur);
        Assert.Equal(695m, line.Gain);
        Assert.Equal(("G01", "US"), (line.Code, line.Country));
        Assert.Equal(new DateTimeOffset(2024, 1, 10, 15, 0, 0, TimeSpan.Zero), line.AcquiredAt);
        Assert.False(line.Flags.HasFlag(GainFlags.ShortTerm));
    }

    [Fact]
    public void Fifo_sells_the_oldest_shares_first_across_purchases()
    {
        // 4 at 100 USD (rate 1.25 → 320 EUR), then 6 at 200 USD (rate 1.00 → 1200 EUR); sell 7 at 300 USD (rate 1.00).
        var result = Build(2025,
            Trade(TradeSide.Buy, 2024, 1, 10, 4, 100m, fees: 1m),
            Trade(TradeSide.Buy, 2024, 6, 10, 6, 200m, fees: 3m),
            Trade(TradeSide.Sell, 2025, 9, 10, 7, 300m, fees: 7m));

        var lines = result.Lines.ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal((4m, 1200m, 320m, 5m), (lines[0].Quantity, lines[0].RealisationEur, lines[0].AcquisitionEur, lines[0].ExpensesEur));   // 1 + 4/7 of 7
        Assert.Equal((3m, 900m, 600m, 4.5m), (lines[1].Quantity, lines[1].RealisationEur, lines[1].AcquisitionEur, lines[1].ExpensesEur)); // 3/6 of 3 + 3/7 of 7
        Assert.Equal(2100m - 920m - 9.5m, result.Balance);
    }

    [Fact]
    public void A_partly_sold_purchase_keeps_its_cost_per_share_for_the_next_sale()
    {
        var result = Build(2025,
            Trade(TradeSide.Buy, 2024, 1, 10, 10, 100m),           // 80 EUR per share
            Trade(TradeSide.Sell, 2025, 3, 10, 4, 125m),           // 100 EUR per share
            Trade(TradeSide.Sell, 2025, 9, 10, 6, 100m));          // 100 EUR per share

        var lines = result.Lines.ToList();
        Assert.Equal([320m, 480m], lines.Select(l => l.AcquisitionEur));
        Assert.Equal([80m, 120m], lines.Select(l => l.Gain));
    }

    [Fact]
    public void Sales_of_other_years_still_consume_their_shares()
    {
        var result = Build(2025,
            Trade(TradeSide.Buy, 2024, 1, 10, 5, 100m),            // 80 EUR per share
            Trade(TradeSide.Buy, 2024, 6, 10, 5, 100m),            // 100 EUR per share
            Trade(TradeSide.Sell, 2024, 6, 10, 5, 100m),           // takes the January shares
            Trade(TradeSide.Sell, 2025, 9, 10, 5, 100m));

        var line = Assert.Single(result.Lines);
        Assert.Equal(new DateTimeOffset(2024, 6, 10, 15, 0, 0, TimeSpan.Zero), line.AcquiredAt);
        Assert.Equal(500m, line.AcquisitionEur);
    }

    [Fact]
    public void Shares_held_less_than_a_year_are_flagged_and_summed_apart()
    {
        var result = Build(2025,
            Trade(TradeSide.Buy, 2025, 3, 10, 10, 100m),           // 800 EUR
            Trade(TradeSide.Sell, 2025, 9, 10, 10, 100m));         // 1000 EUR

        var line = Assert.Single(result.Lines);
        Assert.True(line.Flags.HasFlag(GainFlags.ShortTerm));
        Assert.Equal(184, line.DaysHeld);
        Assert.Equal(200m, result.ShortTermBalance);
    }

    [Fact]
    public void Selling_more_than_was_bought_is_flagged()
    {
        var result = Build(2025,
            Trade(TradeSide.Buy, 2024, 1, 10, 2, 100m),
            Trade(TradeSide.Sell, 2025, 9, 10, 3, 100m));

        var sale = Assert.Single(result.Sales);
        Assert.True(sale.Flags.HasFlag(GainFlags.MissingPurchases));
        Assert.Equal([2m, 1m], sale.Lines.Select(l => l.Quantity));
        Assert.Equal(0m, sale.Lines[1].AcquisitionEur);   // the uncovered share: full sale value, no cost
        Assert.Equal(300m, result.Realisation);
        var shortfall = Assert.Single(result.Shortfalls);
        Assert.Equal((Isin, 1m), (shortfall.Isin, shortfall.Quantity));
    }

    [Fact]
    public void A_split_multiplies_the_shares_held_and_keeps_their_cost()
    {
        // 2 shares at 100 USD (rate 1.25 → 160 EUR); a 3-for-1 split; then 6 shares sold.
        var result = CapitalGains.Build(2025,
            [Trade(TradeSide.Buy, 2024, 1, 10, 2, 100m), Trade(TradeSide.Sell, 2025, 9, 10, 6, 40m)],
            new Dictionary<string, string>(), Rates, _ => "G01",
            [new SplitInput(Isin, new DateOnly(2024, 6, 1), 3m)]);

        var line = Assert.Single(result.Lines);
        Assert.Equal((6m, 160m, 240m), (line.Quantity, line.AcquisitionEur, line.RealisationEur));
        Assert.Empty(result.Shortfalls);
    }

    [Fact]
    public void A_buy_and_a_sell_at_the_same_second_match()
    {
        var result = Build(2025,
            Trade(TradeSide.Sell, 2025, 3, 10, 1, 100m),
            Trade(TradeSide.Buy, 2025, 3, 10, 1, 100m));

        Assert.Equal(GainFlags.ShortTerm, Assert.Single(result.Sales).Flags);
    }

    [Fact]
    public void Missing_rates_are_reported()
    {
        var result = Build(2025,
            Trade(TradeSide.Buy, 2023, 1, 10, 1, 100m),
            Trade(TradeSide.Sell, 2025, 9, 10, 1, 100m));

        Assert.Equal(["USD 2023-01-10"], result.MissingRates);
        Assert.True(Assert.Single(result.Sales).Flags.HasFlag(GainFlags.MissingRate));
    }

    [Fact]
    public void Etfs_get_their_own_code_and_euro_trades_need_no_rate()
    {
        var result = CapitalGains.Build(2025,
            [new(1, "IE00BK5BQT80", "VWCE", "Vanguard", "ETF", TradeSide.Buy, new DateTimeOffset(2023, 2, 1, 0, 0, 0, TimeSpan.Zero), 2, 100m, "EUR", 0, null),
             new(2, "IE00BK5BQT80", "VWCE", "Vanguard", "ETF", TradeSide.Sell, new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero), 2, 120m, "EUR", 0, null)],
            new Dictionary<string, string>(), Rates, type => type == "ETF" ? "G20" : "G01");

        var line = Assert.Single(result.Lines);
        Assert.Equal(("G20", "IE", 40m), (line.Code, line.Country, line.Gain));
        Assert.Empty(result.MissingRates);
    }

    [Theory]
    [InlineData("2023-01-10", "2025-01-09", 0)]       // a day short of two years
    [InlineData("2023-01-10", "2025-01-10", 0.10)]
    [InlineData("2019-01-10", "2025-01-10", 0.20)]
    [InlineData("2016-01-10", "2025-01-10", 0.30)]
    [InlineData("2020-01-10", "2024-06-28", 0)]       // before the rule came into force
    public void Long_holdings_show_their_partial_exclusion(string acquired, string sold, double expected)
    {
        var line = new GainLine("X", "X", null, "G01", "US", DateTimeOffset.Parse(sold), DateTimeOffset.Parse(acquired), 1, 1, 1, 0, GainFlags.None);

        Assert.Equal((decimal)expected, line.LongHoldingExclusion);
    }
}
