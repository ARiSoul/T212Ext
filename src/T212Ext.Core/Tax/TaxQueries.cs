using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Rates;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Tax;

/// <summary>Loads what the IRS report needs and builds it.</summary>
public sealed class TaxQueries(IDbContextFactory<T212ExtDbContext> dbFactory)
{
    private static readonly string[] CashTypes =
        [CashTransactionTypes.InterestOnFreeCash, CashTransactionTypes.LendingInterest, CashTransactionTypes.DividendAdjustment];

    /// <summary>Years with dividends or interest, newest first.</summary>
    public async Task<IReadOnlyList<int>> GetYearsAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dates = await db.Dividends.AsNoTracking().Where(d => accountId == null || d.AccountId == accountId).Select(d => d.PaidOn)
            .Concat(db.CashTransactions.AsNoTracking().Where(t => (accountId == null || t.AccountId == accountId) && CashTypes.Contains(t.Type)).Select(t => t.DateTime))
            .ToListAsync(cancellationToken);
        return [.. dates.Select(d => d.UtcDateTime.Year).Distinct().OrderDescending()];
    }

    public async Task<IrsReportResult> GetReportAsync(int? accountId, int year, string? interestCountry, bool deductRefunds, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var from = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddYears(1);

        var dividends = await db.Dividends.AsNoTracking()
            .Where(d => (accountId == null || d.AccountId == accountId) && d.PaidOn >= from && d.PaidOn < to)
            .Select(d => new IrsDividendInput(d.Id, d.PaidOn, d.Ticker, d.InstrumentName, d.Isin, d.Type, d.Quantity, d.GrossAmountPerShare,
                d.InstrumentCurrency, d.Amount, d.Currency, d.WithholdingTax, d.WithholdingTaxCurrency,
                (d.Sources & DataSources.Api) != 0, d.NetAmountPerShare))
            .ToListAsync(cancellationToken);

        // Refunds for this year arrive in later years: all of them are read, the report keeps this year's.
        var cash = await db.CashTransactions.AsNoTracking()
            .Where(t => (accountId == null || t.AccountId == accountId) && CashTypes.Contains(t.Type)
                && (t.Type == CashTransactionTypes.DividendAdjustment || (t.DateTime >= from && t.DateTime < to)))
            .Select(t => new IrsCashInput(t.DateTime, t.Type, t.Amount, t.Currency, t.Note))
            .ToListAsync(cancellationToken);

        var overrides = await db.InstrumentSettings.AsNoTracking()
            .Where(s => s.SourceCountry != null)
            .ToDictionaryAsync(s => s.Isin, s => s.SourceCountry!, cancellationToken);

        var rateFrom = DateOnly.FromDateTime(from.UtcDateTime).AddDays(-RateTable.MaxGapDays);
        var rates = await db.ExchangeRates.AsNoTracking()
            .Where(r => r.Date >= rateFrom)
            .Select(r => new { r.Currency, r.Date, r.PerEuro })
            .ToListAsync(cancellationToken);

        return IrsReport.Build(
            year,
            dividends,
            cash,
            overrides,
            new RateTable(rates.Select(r => (r.Currency, r.Date, r.PerEuro))),
            interestCountry,
            deductRefunds);
    }
}
