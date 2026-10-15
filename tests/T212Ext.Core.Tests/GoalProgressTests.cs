using Arisoul.T212Ext.Core.Analytics;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Queries;

namespace Arisoul.T212Ext.Core.Tests;

public class GoalProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static Goal Goal(decimal target = 20, bool interest = true) =>
        new() { Name = "Half the rent", MonthlyTarget = target, Currency = "EUR", IncludeInterest = interest };

    private static DividendFact Dividend(DateTimeOffset paidOn, decimal amount) => new(paidOn, amount, 1, "O_US_EQ", "Realty Income", "US7561091049", "USD");

    /// <summary>One payment a month for <paramref name="months"/> months up to now.</summary>
    private static List<DividendFact> Monthly(decimal amount, int months, int startMonthsAgo = 0) =>
        [.. Enumerable.Range(0, months).Select(i => Dividend(Now.AddMonths(-startMonthsAgo - i).AddDays(-1), amount))];

    [Fact]
    public void Current_is_the_last_12_months_income_a_month_against_the_target()
    {
        var status = GoalProgress.Compute(Goal(20), Monthly(10, 12), [], 0, Now);

        Assert.Equal(10m, status.Current);
        Assert.Equal(0.5m, status.Progress);
        Assert.False(status.Reached);
        Assert.Equal(10m, status.Points[^1].MonthlyAverage);
    }

    [Fact]
    public void Interest_counts_only_when_asked_but_withholding_refunds_always_do()
    {
        IncomeFact[] income =
        [
            new(Now.AddMonths(-1), 12, CashTransactionTypes.InterestOnFreeCash),
            new(Now.AddMonths(-2), 12, CashTransactionTypes.DividendAdjustment),
        ];

        Assert.Equal(2m, GoalProgress.Compute(Goal(interest: true), [], income, 0, Now).Current);
        Assert.Equal(1m, GoalProgress.Compute(Goal(interest: false), [], income, 0, Now).Current);
    }

    [Fact]
    public void At_last_years_growth_the_goal_is_reached_around_a_date()
    {
        // 5 a month a year ago, 10 a month now: doubling a year; 40 is two doublings away.
        var dividends = Monthly(5, 12, startMonthsAgo: 12).Concat(Monthly(10, 12)).ToList();

        var status = GoalProgress.Compute(Goal(40), dividends, [], 0, Now);

        Assert.Equal(10m, status.Current);
        Assert.Equal(1m, status.Growth);
        Assert.InRange(status.ReachedAround!.Value, new DateOnly(2028, 10, 1), new DateOnly(2028, 10, 31));
    }

    [Fact]
    public void Reached_or_not_growing_goals_have_no_date()
    {
        Assert.True(GoalProgress.Compute(Goal(10), Monthly(10, 12), [], 0, Now).Reached);
        Assert.Null(GoalProgress.Compute(Goal(10), Monthly(10, 12), [], 0, Now).ReachedAround);
        Assert.Null(GoalProgress.Compute(Goal(40), Monthly(10, 24), [], 0, Now).ReachedAround);   // flat
    }

    [Fact]
    public void The_forward_estimate_adds_the_last_years_interest_when_counted()
    {
        IncomeFact[] interest = [new(Now.AddMonths(-3), 12, CashTransactionTypes.LendingInterest)];

        Assert.Equal(21m, GoalProgress.Compute(Goal(interest: true), [], interest, 240, Now).Forward);
        Assert.Equal(20m, GoalProgress.Compute(Goal(interest: false), [], interest, 240, Now).Forward);
    }

    [Fact]
    public void A_share_of_the_expenses_sets_the_target()
    {
        var goal = new Goal { Name = "Half the expenses", MonthlyExpenses = 1200, CoverShare = 0.5m, Currency = "EUR" };

        GoalQueries.Validate(goal);

        Assert.Equal(600m, goal.MonthlyTarget);
        Assert.Equal(600m, GoalProgress.TargetOf(goal));
    }

    [Theory]
    [InlineData("", 10, null, null)]
    [InlineData("A", 0, null, null)]
    [InlineData("A", 10, 1000.0, 0.0)]
    [InlineData("A", 10, -5.0, 0.5)]
    public void Invalid_goals_are_refused(string name, double target, double? expenses, double? share) =>
        Assert.Throws<ArgumentException>(() => GoalQueries.Validate(new Goal
        {
            Name = name, MonthlyTarget = (decimal)target, Currency = "EUR",
            MonthlyExpenses = (decimal?)expenses, CoverShare = (decimal?)share,
        }));
}
