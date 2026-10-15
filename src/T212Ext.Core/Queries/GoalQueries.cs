using Arisoul.T212Ext.Core.Analytics;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Queries;

/// <summary>The passive income goals, and where each one stands.</summary>
public sealed class GoalQueries(IDbContextFactory<T212ExtDbContext> dbFactory, AnalyticsQueries analytics, TimeProvider time)
{
    private static readonly string[] IncomeTypes =
        [CashTransactionTypes.InterestOnFreeCash, CashTransactionTypes.LendingInterest, CashTransactionTypes.DividendAdjustment];

    public async Task<IReadOnlyList<GoalStatus>> GetStatusesAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var goals = await db.Goals.AsNoTracking().OrderBy(g => g.CreatedAt).ToListAsync(cancellationToken);
        var statuses = new List<GoalStatus>();

        foreach (var currency in goals.Select(g => g.Currency).Distinct())
        {
            var dividends = await db.Dividends.AsNoTracking()
                .Where(d => (accountId == null || d.AccountId == accountId) && d.Currency == currency)
                .Select(d => new DividendFact(d.PaidOn, d.Amount, d.Quantity, d.Ticker, d.InstrumentName, d.Isin, d.InstrumentCurrency))
                .ToListAsync(cancellationToken);
            var income = await db.CashTransactions.AsNoTracking()
                .Where(t => (accountId == null || t.AccountId == accountId) && t.Currency == currency && IncomeTypes.Contains(t.Type))
                .Select(t => new IncomeFact(t.DateTime, t.Amount, t.Type))
                .ToListAsync(cancellationToken);
            var forward = (await analytics.GetDashboardAsync(accountId, currency, cancellationToken))?.ForwardEstimate ?? 0;

            statuses.AddRange(goals.Where(g => g.Currency == currency)
                .Select(g => GoalProgress.Compute(g, dividends, income, forward, time.GetUtcNow())));
        }

        return [.. statuses.OrderBy(s => s.Goal.CreatedAt)];
    }

    /// <summary>Adds the goal (Id 0) or saves its changes.</summary>
    public async Task SaveAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        Validate(goal);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (goal.Id == 0)
        {
            goal.CreatedAt = time.GetUtcNow();
            db.Goals.Add(goal);
        }
        else
        {
            db.Goals.Update(goal);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Goals.Where(g => g.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>A name, a positive target (or positive expenses and a share between 0 and 100 %), and a currency.</summary>
    public static void Validate(Goal goal)
    {
        goal.Name = goal.Name.Trim();
        if (goal.Name.Length is 0 or > 300)
            throw new ArgumentException("A goal needs a name.", nameof(goal));
        if (goal.MonthlyExpenses is not null || goal.CoverShare is not null)
        {
            if (goal.MonthlyExpenses is not > 0 || goal.CoverShare is not (> 0 and <= 10))
                throw new ArgumentException("Expenses must be positive and the share between 0 and 1000 %.", nameof(goal));
            goal.MonthlyTarget = GoalProgress.TargetOf(goal);
        }
        if (goal.MonthlyTarget <= 0)
            throw new ArgumentException("The target must be positive.", nameof(goal));
        if (string.IsNullOrWhiteSpace(goal.Currency))
            throw new ArgumentException("A goal needs a currency.", nameof(goal));
    }
}
