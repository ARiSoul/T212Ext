using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Queries;

public sealed record DividendRow(
    long Id,
    string AccountName,
    DateTimeOffset PaidOn,
    string Ticker,
    string? InstrumentName,
    string? Isin,
    string Type,
    decimal Quantity,
    decimal? GrossAmountPerShare,
    string? InstrumentCurrency,
    decimal Amount,
    string Currency,
    decimal? WithholdingTax,
    string? WithholdingTaxCurrency,
    DataSources Sources);

public sealed record PeriodTotal(int Year, int? Month, decimal Total, int Count);

/// <summary>Totals of one currency. Accounts in different currencies are never added together.</summary>
public sealed record DividendSummary(
    string Currency,
    decimal AllTime,
    decimal YearToDate,
    decimal Last12Months,
    int Count,
    IReadOnlyList<PeriodTotal> ByYear,
    IReadOnlyList<PeriodTotal> ByMonth);

/// <summary>Read side of dividends for the UI. Aggregation is done in memory: a lifetime of dividends is a few thousand rows.</summary>
public sealed class DividendQueries(IDbContextFactory<T212ExtDbContext> dbFactory, TimeProvider time)
{
    /// <param name="instrument">An ISIN or a ticker: only that instrument's dividends.</param>
    public async Task<IReadOnlyList<DividendRow>> GetDividendsAsync(int? accountId, int? year, string? instrument = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Dividends.AsNoTracking().Where(d => accountId == null || d.AccountId == accountId);

        if (!string.IsNullOrEmpty(instrument))
            query = query.Where(d => d.Isin == instrument || d.Ticker == instrument);

        if (year is not null)
        {
            var from = new DateTimeOffset(year.Value, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var to = from.AddYears(1);
            query = query.Where(d => d.PaidOn >= from && d.PaidOn < to);
        }

        return await query
            .OrderByDescending(d => d.PaidOn).ThenBy(d => d.Id)
            .Select(d => new DividendRow(
                d.Id, d.Account!.Name, d.PaidOn, d.Ticker, d.InstrumentName, d.Isin, d.Type, d.Quantity, d.GrossAmountPerShare,
                d.InstrumentCurrency, d.Amount, d.Currency, d.WithholdingTax, d.WithholdingTaxCurrency, d.Sources))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetYearsAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dates = await db.Dividends.AsNoTracking()
            .Where(d => accountId == null || d.AccountId == accountId)
            .Select(d => d.PaidOn)
            .ToListAsync(cancellationToken);
        return [.. dates.Select(d => d.UtcDateTime.Year).Distinct().OrderDescending()];
    }

    public async Task<IReadOnlyList<DividendSummary>> GetSummariesAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Dividends.AsNoTracking()
            .Where(d => accountId == null || d.AccountId == accountId)
            .Select(d => new { d.PaidOn, d.Amount, d.Currency })
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow();
        var startOfYear = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var yearAgo = now.AddYears(-1);

        return [.. rows
            .GroupBy(r => r.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => new DividendSummary(
                g.Key,
                g.Sum(r => r.Amount),
                g.Where(r => r.PaidOn >= startOfYear).Sum(r => r.Amount),
                g.Where(r => r.PaidOn > yearAgo).Sum(r => r.Amount),
                g.Count(),
                [.. g.GroupBy(r => r.PaidOn.UtcDateTime.Year)
                    .OrderBy(y => y.Key)
                    .Select(y => new PeriodTotal(y.Key, null, y.Sum(r => r.Amount), y.Count()))],
                [.. g.GroupBy(r => (r.PaidOn.UtcDateTime.Year, r.PaidOn.UtcDateTime.Month))
                    .OrderBy(m => m.Key)
                    .Select(m => new PeriodTotal(m.Key.Year, m.Key.Month, m.Sum(r => r.Amount), m.Count()))]))];
    }
}
