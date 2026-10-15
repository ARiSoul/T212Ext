using Arisoul.T212Ext.Core.Analytics;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Queries;

/// <summary>Loads the facts the dividend analytics need and computes them, for one currency.</summary>
public sealed class AnalyticsQueries(IDbContextFactory<T212ExtDbContext> dbFactory, TimeProvider time)
{
    private static readonly string[] IncomeTypes =
        [CashTransactionTypes.InterestOnFreeCash, CashTransactionTypes.LendingInterest, CashTransactionTypes.DividendAdjustment];

    /// <summary>The currencies dividends were paid in, most used first. Accounts in different currencies are analysed apart.</summary>
    public async Task<IReadOnlyList<string>> GetCurrenciesAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var currencies = await db.Dividends.AsNoTracking()
            .Where(d => accountId == null || d.AccountId == accountId)
            .GroupBy(d => d.Currency)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return [.. currencies.OrderByDescending(c => c.Count).Select(c => c.Key)];
    }

    /// <summary>
    /// The dividend growth of every instrument, from the dividend per share: the API's gross, else a
    /// CSV row's net plus its withholding (when both are in the instrument's currency).
    /// </summary>
    public async Task<IReadOnlyList<DividendGrowth>> GetGrowthAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Dividends.AsNoTracking()
            .Where(d => accountId == null || d.AccountId == accountId)
            .Select(d => new
            {
                d.Ticker, d.InstrumentName, d.Isin, d.InstrumentCurrency, d.PaidOn, d.Type, d.Quantity,
                d.GrossAmountPerShare, d.NetAmountPerShare, d.WithholdingTax, d.WithholdingTaxCurrency,
            })
            .ToListAsync(cancellationToken);

        var facts = rows
            .Select(r => new
            {
                Row = r,
                PerShare = r.GrossAmountPerShare
                    ?? (r.NetAmountPerShare is { } net
                        ? net + (r.WithholdingTax is { } tax && r.Quantity > 0 && r.WithholdingTaxCurrency == r.InstrumentCurrency ? Math.Abs(tax) / r.Quantity : 0)
                        : (decimal?)null),
            })
            .Where(x => x.PerShare is not null)
            .Select(x => new PerShareFact(x.Row.Ticker, x.Row.InstrumentName, x.Row.Isin, x.Row.InstrumentCurrency,
                DateOnly.FromDateTime(x.Row.PaidOn.UtcDateTime), x.PerShare!.Value, x.Row.Type));

        return DividendGrowthAnalysis.Compute(facts, time.GetUtcNow().Year);
    }

    /// <summary>The dashboard of one currency. Without a currency, the most used one.</summary>
    public async Task<DividendDashboard?> GetDashboardAsync(int? accountId, string? currency = null, CancellationToken cancellationToken = default)
    {
        currency ??= (await GetCurrenciesAsync(accountId, cancellationToken)).FirstOrDefault();
        if (currency is null)
            return null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var dividends = await db.Dividends.AsNoTracking()
            .Where(d => (accountId == null || d.AccountId == accountId) && d.Currency == currency)
            .Select(d => new DividendFact(d.PaidOn, d.Amount, d.Quantity, d.Ticker, d.InstrumentName, d.Isin, d.InstrumentCurrency, d.Type))
            .ToListAsync(cancellationToken);

        var income = await db.CashTransactions.AsNoTracking()
            .Where(t => (accountId == null || t.AccountId == accountId) && t.Currency == currency && IncomeTypes.Contains(t.Type))
            .Select(t => new IncomeFact(t.DateTime, t.Amount, t.Type))
            .ToListAsync(cancellationToken);

        // The latest snapshot of each account in this currency.
        var accountIds = await db.Accounts.AsNoTracking()
            .Where(a => (accountId == null || a.Id == accountId) && a.Currency == currency)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var positions = new List<PositionFact>();
        var snapshots = new List<(DateOnly Date, decimal? Value, decimal? Cost)>();
        foreach (var id in accountIds)
        {
            var latest = await db.Snapshots.AsNoTracking().Include(s => s.Positions)
                .Where(s => s.AccountId == id)
                .OrderByDescending(s => s.Date)
                .FirstOrDefaultAsync(cancellationToken);
            if (latest is not null)
                positions.AddRange(latest.Positions.Select(p => new PositionFact(p.Ticker, p.Name, p.Isin, p.InstrumentCurrency, p.Quantity, p.CurrentValue, p.TotalCost, p.UnrealizedProfitLoss)));

            snapshots.AddRange((await db.Snapshots.AsNoTracking()
                    .Where(s => s.AccountId == id)
                    .Select(s => new { s.Date, s.TotalValue, s.TotalCost })
                    .ToListAsync(cancellationToken))
                .Select(s => (s.Date, s.TotalValue, s.TotalCost)));
        }

        // Several accounts: add their values up per day.
        var portfolio = snapshots
            .GroupBy(s => s.Date)
            .Select(g => new SnapshotFact(g.Key, g.Sum(s => s.Value), g.Sum(s => s.Cost)))
            .ToList();

        return DividendAnalytics.Compute(currency, dividends, income, positions, portfolio, time.GetUtcNow());
    }
}
