using Arisoul.T212Ext.Core.Domain;

namespace Arisoul.T212Ext.Core.Analytics;

/// <summary>The average income a month over the 12 months up to the end of a month.</summary>
public sealed record GoalPoint(int Year, int Month, decimal MonthlyAverage);

/// <summary>Where a goal stands.</summary>
/// <param name="Current">The average income a month over the last 12 months.</param>
/// <param name="Forward">The next 12 months' estimate a month (dividends from today's positions, plus the last 12 months' interest when counted).</param>
/// <param name="Growth">How much <paramref name="Current"/> grew against a year before; null without a year of history.</param>
/// <param name="ReachedAround">When the goal would be reached if <paramref name="Growth"/> held; null when reached, not growing, or too far.</param>
public sealed record GoalStatus(
    Goal Goal,
    decimal Target,
    decimal Current,
    decimal Forward,
    decimal Progress,
    decimal? Growth,
    DateOnly? ReachedAround,
    IReadOnlyList<GoalPoint> Points)
{
    public bool Reached => Current >= Target;
}

/// <summary>
/// Progress towards a passive income goal. The measure is the last 12 months' income a month, so
/// months with and without dividends even out. The projection assumes the last year's growth goes on,
/// which includes the money invested in that year: it is a pace, not a promise.
/// </summary>
public static class GoalProgress
{
    /// <summary>Beyond this, "when" is not worth saying.</summary>
    public const int MaxYears = 50;

    public static decimal TargetOf(Goal goal) =>
        goal.MonthlyExpenses is { } expenses && goal.CoverShare is { } share ? Math.Round(expenses * share, 2) : goal.MonthlyTarget;

    public static GoalStatus Compute(Goal goal, IReadOnlyList<DividendFact> dividends, IReadOnlyList<IncomeFact> income, decimal forwardDividends12, DateTimeOffset now)
    {
        var target = TargetOf(goal);

        // Refunds of withholding are dividend income; interest and lending only when the goal counts them.
        var amounts = dividends.Select(d => (d.PaidOn, d.Amount))
            .Concat(income.Where(i => i.Type == CashTransactionTypes.DividendAdjustment || goal.IncludeInterest).Select(i => (PaidOn: i.DateTime, i.Amount)))
            .ToList();

        var points = new List<GoalPoint>();
        if (amounts.Count > 0)
        {
            var first = amounts.Min(a => a.PaidOn);
            for (var month = new DateTime(first.Year, first.Month, 1); month <= new DateTime(now.Year, now.Month, 1); month = month.AddMonths(1))
            {
                var end = new DateTimeOffset(month.AddMonths(1), TimeSpan.Zero);
                var end12 = end > now ? now : end;
                var start = end12.AddYears(-1);
                var total = amounts.Where(a => a.PaidOn > start && a.PaidOn <= end12).Sum(a => a.Amount);
                points.Add(new GoalPoint(month.Year, month.Month, Math.Round(total / 12, 2)));
            }
        }

        var current = points.Count > 0 ? points[^1].MonthlyAverage : 0;
        var interest12 = goal.IncludeInterest
            ? income.Where(i => i.Type != CashTransactionTypes.DividendAdjustment && i.DateTime > now.AddYears(-1) && i.DateTime <= now).Sum(i => i.Amount)
            : 0;
        var forward = Math.Round((forwardDividends12 + interest12) / 12, 2);

        // Against the same point a year before, when there is one with income.
        decimal? growth = null;
        if (points.Count > 12 && points[^13].MonthlyAverage > 0)
            growth = Math.Round(current / points[^13].MonthlyAverage - 1, 4);

        DateOnly? reachedAround = null;
        if (current > 0 && current < target && growth > 0)
        {
            var years = Math.Log((double)(target / current)) / Math.Log(1 + (double)growth.Value);
            if (years <= MaxYears)
                reachedAround = DateOnly.FromDateTime(now.UtcDateTime.AddDays(years * 365.25));
        }

        return new GoalStatus(goal, target, current, forward, target > 0 ? Math.Round(current / target, 4) : 0, growth, reachedAround, points);
    }
}
