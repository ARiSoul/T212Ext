using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Rates;
using Arisoul.T212Ext.Core.Tax;

namespace Arisoul.T212Ext.Core.Tests;

public class IrsReportTests
{
    // Friday 14 and Monday 17 March 2025.
    private static readonly RateTable Rates = new(
    [
        ("USD", new DateOnly(2025, 3, 14), 1.0889m),
        ("USD", new DateOnly(2025, 3, 17), 1.0903m),
        ("GBP", new DateOnly(2025, 3, 14), 0.8400m),
        ("CHF", new DateOnly(2025, 3, 14), 0.9600m),
    ]);

    private static IrsDividendInput Dividend(
        int day, decimal quantity, decimal? grossPerShare, string currency, decimal net, decimal? withheld,
        string isin = "US0378331005", string type = "ORDINARY", long id = 1) =>
        new(id, new DateTimeOffset(2025, 3, day, 10, 0, 0, TimeSpan.Zero), "T", "Name", isin, type, quantity, grossPerShare,
            currency, net, "EUR", withheld, withheld is null ? null : currency);

    private static IrsReportResult Build(
        IReadOnlyList<IrsDividendInput> dividends,
        IReadOnlyList<IrsCashInput>? cash = null,
        IReadOnlyDictionary<string, string>? overrides = null,
        bool deductRefunds = true) =>
        IrsReport.Build(2025, dividends, cash ?? [], overrides ?? new Dictionary<string, string>(), Rates, "CY", deductRefunds);

    [Fact]
    public void Gross_and_tax_paid_abroad_are_in_euros_at_the_payment_date_rate()
    {
        // 10 shares × 1.0889 USD = 10.889 USD = 10.00 EUR; 15 % withheld = 1.63335 USD = 1.50 EUR; net 8.50 EUR.
        var report = Build([Dividend(14, 10, 1.0889m, "USD", 8.50m, 1.63335m)]);

        var line = Assert.Single(report.Lines);
        Assert.Equal((801, "E11", "US", 840), (line.Number, line.Code, line.Country, line.CountryNumeric));
        Assert.Equal(10.00m, line.Gross);
        Assert.Equal(1.50m, line.TaxPaidAbroad);
        Assert.Equal(IrsFlags.None, Assert.Single(report.Dividends).Flags);
    }

    [Fact]
    public void A_payment_on_a_weekend_uses_the_last_quote_before_it()
    {
        var report = Build([Dividend(16, 10, 1.0889m, "USD", 8.50m, 1.63335m)]);   // Sunday

        var dividend = Assert.Single(report.Dividends);
        Assert.Equal(new DateOnly(2025, 3, 14), dividend.RateDate);
        Assert.Equal(10.00m, dividend.GrossEur);
    }

    [Fact]
    public void Pence_are_converted_as_hundredths_of_a_pound()
    {
        // 100 shares × 8.4 GBX = 840 GBX = 8.40 GBP = 10.00 EUR.
        var report = Build([Dividend(14, 100, 8.4m, "GBX", 10.00m, 0m, isin: "GB0002875804")]);

        Assert.Equal(10.00m, Assert.Single(report.Lines).Gross);
    }

    [Fact]
    public void One_line_per_country_largest_first()
    {
        var report = Build(
        [
            Dividend(14, 10, 1.0889m, "USD", 8.50m, 1.63335m, id: 1),
            Dividend(14, 5, 1.0889m, "USD", 4.25m, 0.816675m, id: 2),
            Dividend(14, 20, 0.96m, "CHF", 13.00m, 6.72m, isin: "CH0038863350", id: 3),
        ]);

        Assert.Equal([("CH", 801, 20.00m), ("US", 802, 15.00m)], report.Lines.Select(l => (l.Country!, l.Number, l.Gross)));
        Assert.Equal(2, report.Lines[1].Count);
        Assert.Equal(35.00m, report.TotalGross);
    }

    [Fact]
    public void Without_withholding_from_a_report_it_is_estimated_from_gross_and_net()
    {
        var dividend = Assert.Single(Build([Dividend(14, 10, 1.0889m, "USD", 8.50m, null)]).Dividends);

        Assert.Equal(1.50m, dividend.WithheldEur);
        Assert.True(dividend.Flags.HasFlag(IrsFlags.WithholdingEstimated));
    }

    [Fact]
    public void Gross_minus_tax_far_from_net_is_flagged()
    {
        var dividend = Assert.Single(Build([Dividend(14, 10, 1.0889m, "USD", 7.00m, 1.63335m)]).Dividends);

        Assert.True(dividend.Flags.HasFlag(IrsFlags.Mismatch));
    }

    [Fact]
    public void Withholding_above_the_treaty_rate_is_flagged()
    {
        var dividend = Assert.Single(Build([Dividend(14, 10, 1.0889m, "USD", 7.00m, 3.2667m)]).Dividends);

        Assert.True(dividend.Flags.HasFlag(IrsFlags.HighWithholding));
        Assert.Equal(0.30m, dividend.EffectiveRate);
    }

    [Fact]
    public void Returns_of_capital_are_shown_but_left_out_of_the_lines()
    {
        var report = Build(
        [
            Dividend(14, 10, 1.0889m, "USD", 8.50m, 1.63335m, id: 1),
            Dividend(14, 10, 1.0889m, "USD", 10.00m, 0m, type: "RETURN_OF_CAPITAL_NON_US", id: 2),
        ]);

        Assert.Equal(2, report.Dividends.Count);
        Assert.Equal(10.00m, Assert.Single(report.Lines).Gross);
        Assert.True(report.Dividends[1].Flags.HasFlag(IrsFlags.ReturnOfCapital));
    }

    [Fact]
    public void The_source_country_override_wins_and_international_isins_are_flagged()
    {
        var report = Build(
            [Dividend(14, 10, 1.0889m, "USD", 8.50m, 1.63335m, isin: "XS1234567890", id: 1),
             Dividend(14, 10, 1.0889m, "USD", 8.50m, 1.63335m, isin: "IE00BK5BQT80", id: 2)],
            overrides: new Dictionary<string, string> { ["IE00BK5BQT80"] = "US" });

        Assert.True(report.Dividends[0].Flags.HasFlag(IrsFlags.NoCountry));
        Assert.Equal("US", report.Dividends[1].Country);
        Assert.Null(report.Lines.Single(l => l.Country == "XS").CountryNumeric);
    }

    [Fact]
    public void Withholding_refunds_for_the_year_are_deducted_from_that_country_when_asked()
    {
        IrsCashInput Refund(int year, decimal amount, string note) =>
            new(new DateTimeOffset(year, 6, 13, 0, 0, 0, TimeSpan.Zero), CashTransactionTypes.DividendAdjustment, amount, "EUR", note);

        IReadOnlyList<IrsCashInput> cash =
        [
            Refund(2026, 0.40m, "2025 US dividends withholding tax adjustment."),
            Refund(2026, 9.99m, "2024 US dividends withholding tax adjustment"),
        ];
        IReadOnlyList<IrsDividendInput> dividends = [Dividend(14, 10, 1.0889m, "USD", 8.50m, 1.63335m)];

        var deducted = Build(dividends, cash);
        var kept = Build(dividends, cash, deductRefunds: false);

        var refund = Assert.Single(deducted.Refunds);
        Assert.Equal((2025, "US", 0.40m), (refund.ForYear!.Value, refund.Country!, refund.AmountEur));
        Assert.Equal(1.10m, deducted.Lines[0].TaxPaidAbroad);
        Assert.Equal(0.40m, deducted.Lines[0].RefundsDeducted);
        Assert.Equal(1.50m, kept.Lines[0].TaxPaidAbroad);
    }

    [Fact]
    public void Interest_and_share_lending_are_listed_apart_with_the_chosen_country()
    {
        var report = Build([],
        [
            new(new DateTimeOffset(2025, 3, 14, 0, 0, 0, TimeSpan.Zero), CashTransactionTypes.LendingInterest, 0.02m, "EUR", null),
            new(new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero), CashTransactionTypes.LendingInterest, 0.03m, "EUR", null),
            new(new DateTimeOffset(2025, 3, 14, 0, 0, 0, TimeSpan.Zero), CashTransactionTypes.InterestOnFreeCash, 1.00m, "EUR", null),
            new(new DateTimeOffset(2024, 3, 14, 0, 0, 0, TimeSpan.Zero), CashTransactionTypes.InterestOnFreeCash, 5.00m, "EUR", null),
        ]);

        var lending = report.OtherIncome.Single(o => o.Type == CashTransactionTypes.LendingInterest);
        Assert.Equal(("E21", "CY", 196, 0.05m, 2), (lending.SuggestedCode, lending.Country!, lending.CountryNumeric!.Value, lending.AmountEur, lending.Count));
        Assert.Equal(1.00m, report.OtherIncome.Single(o => o.Type == CashTransactionTypes.InterestOnFreeCash).AmountEur);
    }

    [Fact]
    public void Missing_rates_are_reported_and_the_dividend_left_out_of_the_lines()
    {
        var report = Build([Dividend(14, 10, 1m, "JPY", 8.50m, 1m, isin: "JP3633400001")]);

        Assert.Equal(["JPY 2025-03-14"], report.MissingRates);
        Assert.True(Assert.Single(report.Dividends).Flags.HasFlag(IrsFlags.MissingRate));
        Assert.Empty(report.Lines);
    }

    [Fact]
    public void With_the_api_gross_and_the_report_net_the_withholding_is_exact()
    {
        // 0.0483283 shares: gross 0.25, net 0.2125 USD per share. The report rounded the tax to 0.00.
        var dividend = Dividend(14, 0.0483283m, 0.25m, "USD", 0.01m, 0.00m) with { NetPerShare = 0.2125m };

        var result = Assert.Single(Build([dividend]).Dividends);

        Assert.Equal(0.15m, result.EffectiveRate);
        Assert.False(result.Flags.HasFlag(IrsFlags.HighWithholding));
    }

    [Fact]
    public void Small_dividends_do_not_look_over_withheld_because_of_rounding_to_cents()
    {
        // 15 % of 0.3727 USD is 0.0559; in euros 0.34 and 0.05 would read as 14.7 % or 15.6 %.
        var dividend = Dividend(14, 1.4908m, 0.25m, "USD", 0.29m, 0.0559m);

        Assert.False(Assert.Single(Build([dividend]).Dividends).Flags.HasFlag(IrsFlags.HighWithholding));
    }

    [Fact]
    public void Without_the_api_gross_the_figures_are_marked_as_derived()
    {
        var dividend = Dividend(14, 10, 1.0889m, "USD", 8.50m, 1.63335m) with { GrossFromApi = false };

        Assert.True(Assert.Single(Build([dividend]).Dividends).Flags.HasFlag(IrsFlags.GrossDerived));
    }

    [Fact]
    public void Portuguese_dividends_are_set_apart_from_anexo_j()
    {
        var report = Build(
        [
            Dividend(14, 10, 1.0889m, "USD", 8.50m, 1.63335m, id: 1),
            Dividend(14, 10, 0.366m, "EUR", 2.38m, 1.28m, isin: "PTZON0AM0006", id: 2),
        ]);

        Assert.Equal(["US"], report.Lines.Select(l => l.Country));
        Assert.Equal(new IrsDomestic(1, 3.66m, 1.28m), report.Domestic);
        Assert.Equal(IrsFlags.None, report.Dividends[1].Flags);   // 35 % in Portugal is not a treaty question
    }

    [Theory]
    [InlineData("2024 US dividends withholding tax adjustment", 2024)]
    [InlineData("2025 US dividends withholding tax refund adjustment.", 2025)]
    [InlineData("Adjustment", null)]
    [InlineData(null, null)]
    public void The_year_of_a_refund_comes_from_its_note(string? note, int? year) =>
        Assert.Equal(year, IrsReport.YearOf(note));
}
