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

    /// <summary>Capital gains of a year by FIFO, from the API's filled orders.</summary>
    /// <returns>The result, and the corporate-action fills (splits, spin-offs…) it cannot handle yet.</returns>
    public async Task<(CapitalGainsResult Result, IReadOnlyList<string> Unsupported)> GetCapitalGainsAsync(int? accountId, int year, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var orders = await db.Orders.AsNoTracking()
            .Where(o => (accountId == null || o.AccountId == accountId) && o.FillId != null && o.FilledAt != null && o.FillQuantity != null && o.FillPrice != null)
            .Select(o => new
            {
                o.Id, o.Isin, o.Ticker, o.InstrumentName, o.Side, o.FilledAt, o.FillQuantity, o.FillPrice, o.InstrumentCurrency,
                o.FillType, o.RealisedProfitLoss, o.WalletCurrency, Fees = o.Taxes.Sum(t => t.Quantity),
            })
            .ToListAsync(cancellationToken);

        var tickers = orders.Select(o => o.Ticker).Distinct().ToList();
        var types = await db.Instruments.AsNoTracking()
            .Where(i => tickers.Contains(i.Ticker))
            .ToDictionaryAsync(i => i.Ticker, i => i.Type, cancellationToken);

        var unsupported = orders
            .Where(o => o.FillType is not null and not "TRADE")
            .Select(o => $"{o.FilledAt:yyyy-MM-dd} {o.Ticker} {o.FillType}")
            .ToList();

        var trades = orders
            .Where(o => o.FillType is null or "TRADE")
            .Select(o => new TradeInput(
                o.Id,
                o.Isin ?? o.Ticker,
                o.Ticker,
                o.InstrumentName,
                types.GetValueOrDefault(o.Ticker),
                o.Side == "SELL" ? TradeSide.Sell : TradeSide.Buy,
                o.FilledAt!.Value,
                Math.Abs(o.FillQuantity!.Value),
                o.FillPrice!.Value,
                o.InstrumentCurrency ?? "EUR",
                // Fees come negative, in the account currency (euros for a euro account).
                Math.Abs(o.Fees),
                o.Side == "SELL" ? o.RealisedProfitLoss : null))
            .ToList();

        var overrides = await db.InstrumentSettings.AsNoTracking()
            .Where(s => s.SourceCountry != null)
            .ToDictionaryAsync(s => s.Isin, s => s.SourceCountry!, cancellationToken);

        var rates = await db.ExchangeRates.AsNoTracking()
            .Select(r => new { r.Currency, r.Date, r.PerEuro })
            .ToListAsync(cancellationToken);

        var splits = await db.StockSplits.AsNoTracking()
            .Select(s => new SplitInput(s.Isin, s.Date, s.Ratio))
            .ToListAsync(cancellationToken);

        var result = CapitalGains.Build(year, trades, overrides, new RateTable(rates.Select(r => (r.Currency, r.Date, r.PerEuro))), CapitalGainCodes.ForInstrumentType, splits);
        return (result, unsupported);
    }

    /// <summary>The instruments with filled orders, for picking one (splits).</summary>
    public async Task<IReadOnlyList<(string Isin, string Ticker, string? Name)>> GetTradedInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Orders.AsNoTracking()
            .Where(o => o.FillId != null && o.Isin != null)
            .GroupBy(o => o.Isin!)
            .Select(g => new { Isin = g.Key, Ticker = g.Max(o => o.Ticker), Name = g.Max(o => o.InstrumentName) })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => (r.Isin, r.Ticker ?? r.Isin, r.Name)).OrderBy(r => r.Name ?? r.Item2)];
    }

    /// <summary>Years with sales, newest first.</summary>
    public async Task<IReadOnlyList<int>> GetSaleYearsAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var dates = await db.Orders.AsNoTracking()
            .Where(o => (accountId == null || o.AccountId == accountId) && o.Side == "SELL" && o.FillId != null && o.FilledAt != null)
            .Select(o => o.FilledAt!.Value)
            .ToListAsync(cancellationToken);
        return [.. dates.Select(d => d.UtcDateTime.Year).Distinct().OrderDescending()];
    }

    public async Task<IrsReportResult> GetReportAsync(int? accountId, int year, string? interestCountry, bool deductRefunds, bool capAtTreaty = true, CancellationToken cancellationToken = default)
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
            deductRefunds,
            capAtTreaty);
    }
}
