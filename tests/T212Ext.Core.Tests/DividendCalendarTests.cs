using Arisoul.T212Ext.Core.Analytics;

namespace Arisoul.T212Ext.Core.Tests;

public class DividendCalendarTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private const string Isin = "US7561091049";

    private static DividendFact Paid(string date, decimal perShare, decimal quantity = 10, string type = "ORDINARY", string isin = Isin) =>
        new(DateTimeOffset.Parse(date + "T10:00:00+00:00"), perShare * quantity, quantity, "O_US_EQ", "Realty Income", isin, "USD", type);

    private static PositionFact Held(decimal quantity, string isin = Isin) => new("O_US_EQ", "Realty Income", isin, "USD", quantity, null, null, null);

    private static IReadOnlyList<ExpectedPayment> Project(IReadOnlyList<DividendFact> paid, decimal held = 20) =>
        DividendCalendar.Project(paid, [Held(held)], Now);

    private static DateOnly D(string date) => DateOnly.Parse(date);

    [Fact]
    public void A_quarterly_payer_is_expected_a_year_after_each_payment_at_the_latest_amount()
    {
        var expected = Project([Paid("2025-11-15", 0.25m), Paid("2026-02-15", 0.25m), Paid("2026-05-15", 0.26m), Paid("2026-08-15", 0.27m)]);

        Assert.Equal([D("2026-11-15"), D("2027-02-15"), D("2027-05-15"), D("2027-08-15")], expected.Select(e => e.ExpectedOn));
        Assert.All(expected, e =>
        {
            Assert.Equal(0.27m, e.PerShare);
            Assert.Equal(5.4m, e.Amount);   // 20 shares held today
            Assert.Equal(ExpectedStatus.Estimated, e.Status);
        });
        Assert.Equal(D("2025-11-15"), expected[0].BasedOn);
    }

    [Fact]
    public void Interim_and_final_dividends_keep_their_own_amounts()
    {
        var expected = Project([Paid("2024-11-10", 0.45m), Paid("2025-05-10", 1.4m), Paid("2025-11-10", 0.5m), Paid("2026-05-10", 1.5m)], held: 10);

        Assert.Equal([(D("2026-11-10"), 5m), (D("2027-05-10"), 15m)], expected.Select(e => (e.ExpectedOn, e.Amount)));
    }

    [Fact]
    public void A_payment_already_made_again_is_not_expected_twice()
    {
        // Paid a week early this year: last year's slot is already covered.
        var expected = Project([Paid("2025-10-20", 1m), Paid("2026-10-12", 1m)]);

        Assert.Equal(D("2027-10-12"), Assert.Single(expected).ExpectedOn);
    }

    [Fact]
    public void A_date_just_passed_is_due_and_one_well_passed_is_late()
    {
        Assert.Equal(ExpectedStatus.Due, Assert.Single(Project([Paid("2025-10-05", 1m)])).Status);

        // Quarterly, last paid in May: the August payment never came, and nothing is expected after it.
        var late = Assert.Single(Project([Paid("2025-12-01", 0.3m), Paid("2026-03-01", 0.3m), Paid("2026-05-20", 0.3m)]));
        Assert.Equal(ExpectedStatus.Late, late.Status);
        Assert.Equal(D("2026-08-13"), late.ExpectedOn);   // 85 days, the usual gap, after the last one
    }

    [Fact]
    public void A_holding_that_stopped_paying_or_is_not_held_is_not_expected()
    {
        Assert.Empty(Project([Paid("2025-03-01", 1m), Paid("2025-06-01", 1m)]));
        Assert.Empty(Project([Paid("2026-08-15", 1m)], held: 0));
        Assert.Empty(DividendCalendar.Project([Paid("2026-08-15", 1m)], [Held(10, "IE00B3XXRP09")], Now));
    }

    [Fact]
    public void Special_bonus_and_capital_gains_payments_are_left_out()
    {
        var monthly = Enumerable.Range(0, 13).Select(i => Paid(new DateOnly(2025, 9, 15).AddMonths(i).ToString("yyyy-MM-dd"), 0.1m)).ToList();
        monthly.Add(Paid("2026-03-20", 1m));                              // special: ten times the usual
        monthly.Add(Paid("2026-06-25", 0.05m, type: "BONUS"));
        monthly.Add(Paid("2025-12-28", 0.4m, type: "LONG_TERM_CAPITAL_GAINS"));

        var expected = Project(monthly);

        Assert.Equal(12, expected.Count);
        Assert.Equal(D("2026-10-15"), expected[0].ExpectedOn);
        Assert.All(expected, e => Assert.Equal(0.1m, e.PerShare));
    }

    [Fact]
    public void Under_a_year_of_history_the_usual_gap_fills_in()
    {
        // Bought in the summer: two monthly payments so far, a year of them expected.
        var monthly = Project([Paid("2026-08-15", 0.1m), Paid("2026-09-15", 0.1m)]);
        Assert.Equal(12, monthly.Count);
        Assert.Equal(D("2026-10-16"), monthly[0].ExpectedOn);

        // Quarterly since December: September's payment is due, then four in the next 12 months.
        var quarterly = Project([Paid("2025-12-11", 0.4m), Paid("2026-03-11", 0.4m), Paid("2026-06-09", 0.4m)]);
        Assert.Equal((D("2026-09-07"), ExpectedStatus.Due), (quarterly[0].ExpectedOn, quarterly[0].Status));
        Assert.Equal([D("2026-12-11"), D("2027-03-11"), D("2027-06-09"), D("2027-09-02")],
            quarterly.Where(e => e.Status == ExpectedStatus.Estimated).Select(e => e.ExpectedOn));
    }

    [Fact]
    public void Rows_of_one_payment_add_up()
    {
        // Two accounts, or ordinary and return of capital, on the same day.
        var expected = Project([Paid("2026-08-15", 0.2m, quantity: 10), Paid("2026-08-15", 0.1m, quantity: 10, type: "RETURN_OF_CAPITAL")], held: 10);

        Assert.Equal(3m, Assert.Single(expected).Amount);   // 0.3 a share
    }
}
