using Arisoul.T212Ext.Core.Analytics;
using Arisoul.T212Ext.Core.Rates;

namespace Arisoul.T212Ext.Core.Tax;

public enum TradeSide
{
    Buy,
    Sell,
}

/// <summary>One executed trade. Quantity is always positive; fees are in euros (the account's currency).</summary>
public sealed record TradeInput(
    long Id,
    string Isin,
    string Ticker,
    string? Name,
    string? InstrumentType,
    TradeSide Side,
    DateTimeOffset ExecutedAt,
    decimal Quantity,
    decimal Price,
    string Currency,
    decimal FeesEur,
    decimal? BrokerResult);

/// <summary>What may need a look before a sale is used.</summary>
[Flags]
public enum GainFlags
{
    None = 0,

    /// <summary>Held less than 365 days: may have to be aggregated (englobamento), see TAX.md.</summary>
    ShortTerm = 1,

    /// <summary>The sale is larger than the shares bought before it: the purchase history is incomplete.</summary>
    MissingPurchases = 2,

    /// <summary>No exchange rate for a date of the sale or of the purchase.</summary>
    MissingRate = 4,

    /// <summary>No source country (an international ISIN): set it on the instrument.</summary>
    NoCountry = 8,
}

/// <summary>
/// One part of a sale matched to one purchase (FIFO): what a line of Anexo J, quadro 9.2A, needs.
/// Values are in euros at the ECB rate of each date.
/// </summary>
public sealed record GainLine(
    string Isin,
    string Ticker,
    string? Name,
    string Code,
    string? Country,
    DateTimeOffset SoldAt,
    DateTimeOffset AcquiredAt,
    decimal Quantity,
    decimal RealisationEur,
    decimal AcquisitionEur,
    decimal ExpensesEur,
    GainFlags Flags)
{
    public decimal Gain => RealisationEur - AcquisitionEur - ExpensesEur;
    public int DaysHeld => SoldAt.UtcDateTime.Date.Subtract(AcquiredAt.UtcDateTime.Date).Days;

    /// <summary>
    /// The share of a gain excluded for long holdings of listed securities (art. 43.º n.º 5, since
    /// 29 June 2024): 10 % from 2 years, 20 % from 5, 30 % from 8. Informative: see TAX.md.
    /// </summary>
    public decimal LongHoldingExclusion
    {
        get
        {
            var acquired = AcquiredAt.UtcDateTime.Date;
            var sold = SoldAt.UtcDateTime.Date;
            if (sold < new DateTime(2024, 6, 29) || sold < acquired.AddYears(2))
                return 0m;
            if (sold >= acquired.AddYears(8))
                return 0.30m;
            return sold >= acquired.AddYears(5) ? 0.20m : 0.10m;
        }
    }
}

/// <summary>A split to apply before the trades of its date: every share held becomes <see cref="Ratio"/> shares.</summary>
public sealed record SplitInput(string Isin, DateOnly Date, decimal Ratio);

/// <summary>An instrument whose sales went beyond the shares bought: most often a split not entered yet.</summary>
public sealed record Shortfall(string Isin, string Ticker, string? Name, DateTimeOffset FirstSale, decimal Quantity);

/// <summary>One sale with its FIFO lines and Trading 212's own result (average cost) to compare.</summary>
public sealed record SaleResult(TradeInput Sale, IReadOnlyList<GainLine> Lines, GainFlags Flags)
{
    public decimal Gain => Lines.Sum(l => l.Gain);
}

public sealed record CapitalGainsResult(
    int Year,
    IReadOnlyList<SaleResult> Sales,
    IReadOnlyList<string> MissingRates,
    IReadOnlyList<Shortfall> Shortfalls)
{
    public IEnumerable<GainLine> Lines => Sales.SelectMany(s => s.Lines);
    public decimal Realisation => Lines.Sum(l => l.RealisationEur);
    public decimal Acquisition => Lines.Sum(l => l.AcquisitionEur);
    public decimal Expenses => Lines.Sum(l => l.ExpensesEur);

    /// <summary>The year's balance (saldo): gains minus losses.</summary>
    public decimal Balance => Lines.Sum(l => l.Gain);

    /// <summary>The balance of what was held less than 365 days.</summary>
    public decimal ShortTermBalance => Lines.Where(l => l.DaysHeld < 365).Sum(l => l.Gain);

    public decimal? BrokerResult => Sales.All(s => s.Sale.BrokerResult is null) ? null : Sales.Sum(s => s.Sale.BrokerResult ?? 0);
}

/// <summary>
/// Capital gains of a tax year by FIFO: each sale consumes the oldest shares bought first
/// (Código do IRS, art. 43.º). Every purchase and sale is converted to euros at the ECB rate of
/// its own date; purchase fees go with the shares they bought, sale fees with the sale, both as
/// expenses (art. 51.º). This prepares figures to check, it is not tax advice; see docs/TAX.md.
/// </summary>
public static class CapitalGains
{
    /// <summary>Quantities below this are rounding left-overs of fractional shares.</summary>
    private const decimal Dust = 0.000001m;

    public static CapitalGainsResult Build(
        int year,
        IReadOnlyList<TradeInput> trades,
        IReadOnlyDictionary<string, string> sourceCountryByIsin,
        RateTable rates,
        Func<string?, string> codeForType,
        IReadOnlyList<SplitInput>? splits = null)
    {
        var missingRates = new SortedSet<string>(StringComparer.Ordinal);
        var sales = new List<SaleResult>();
        var shortfalls = new List<Shortfall>();

        foreach (var instrument in trades.GroupBy(t => t.Isin))
        {
            var lots = new LinkedList<Lot>();
            var country = sourceCountryByIsin.TryGetValue(instrument.Key, out var chosen) ? chosen : DividendAnalytics.CountryOf(instrument.Key);
            var pendingSplits = new Queue<SplitInput>((splits ?? []).Where(s => s.Isin == instrument.Key && s.Ratio > 0).OrderBy(s => s.Date));
            Shortfall? shortfall = null;

            // Buys before sells at the same instant, so a same-second round trip finds its shares.
            foreach (var trade in instrument.OrderBy(t => t.ExecutedAt).ThenBy(t => t.Side))
            {
                // A split multiplies the shares held; their cost stays the same.
                while (pendingSplits.TryPeek(out var split) && split.Date <= DateOnly.FromDateTime(trade.ExecutedAt.UtcDateTime))
                {
                    foreach (var held in lots)
                        held.Remaining *= split.Ratio;
                    pendingSplits.Dequeue();
                }

                var value = ToEuro(trade.Quantity * trade.Price, trade.Currency, trade.ExecutedAt, rates, missingRates);

                if (trade.Side == TradeSide.Buy)
                {
                    lots.AddLast(new Lot(trade.ExecutedAt, trade.Quantity, value, trade.FeesEur, value is null));
                    continue;
                }

                var result = Consume(trade, value, lots, country, codeForType(trade.InstrumentType));
                if (result.Flags.HasFlag(GainFlags.MissingPurchases))
                {
                    // Only the uncovered line carries the flag (it is set after the lots run out).
                    var missing = result.Lines.Where(l => l.Flags.HasFlag(GainFlags.MissingPurchases)).Sum(l => l.Quantity);
                    shortfall = shortfall is null
                        ? new Shortfall(instrument.Key, trade.Ticker, trade.Name, trade.ExecutedAt, missing)
                        : shortfall with { Quantity = shortfall.Quantity + missing };
                }

                if (trade.ExecutedAt.UtcDateTime.Year == year)
                    sales.Add(result);
            }

            if (shortfall is not null)
                shortfalls.Add(shortfall);
        }

        return new CapitalGainsResult(year, [.. sales.OrderBy(s => s.Sale.ExecutedAt)], [.. missingRates], [.. shortfalls.OrderBy(s => s.FirstSale)]);
    }

    private static SaleResult Consume(TradeInput sale, decimal? proceeds, LinkedList<Lot> lots, string? country, string code)
    {
        var flags = GainFlags.None;
        if (country is null || CountryCodes.Numeric(country) is null)
            flags |= GainFlags.NoCountry;
        if (proceeds is null)
            flags |= GainFlags.MissingRate;

        var lines = new List<GainLine>();
        var remaining = sale.Quantity;

        while (remaining > Dust && lots.First is { } node)
        {
            var lot = node.Value;
            var taken = Math.Min(lot.Remaining, remaining);
            var lotShare = taken / lot.Remaining;
            var saleShare = taken / sale.Quantity;

            var acquisition = lot.Cost is { } cost ? Round(cost * lotShare) : 0;
            var expenses = Round(lot.Fees * lotShare + sale.FeesEur * saleShare);
            var realisation = proceeds is { } p ? Round(p * saleShare) : 0;

            var lineFlags = flags | (lot.MissingRate ? GainFlags.MissingRate : GainFlags.None);
            var line = new GainLine(sale.Isin, sale.Ticker, sale.Name, code, country, sale.ExecutedAt, lot.AcquiredAt, taken, realisation, acquisition, expenses, lineFlags);
            if (line.DaysHeld < 365)
                line = line with { Flags = line.Flags | GainFlags.ShortTerm };
            lines.Add(line);

            // What is left of the lot keeps its cost per share.
            lot.Remaining -= taken;
            lot.Cost = lot.Cost is { } c ? c - (c * lotShare) : null;
            lot.Fees -= lot.Fees * lotShare;
            remaining -= taken;
            if (lot.Remaining <= Dust)
                lots.RemoveFirst();
        }

        // Shares sold that no purchase covers: a line with no acquisition value, so the sale value
        // is complete and the gap shows (most often a split not entered yet).
        if (remaining > Dust)
        {
            flags |= GainFlags.MissingPurchases;
            var share = remaining / sale.Quantity;
            lines.Add(new GainLine(sale.Isin, sale.Ticker, sale.Name, code, country, sale.ExecutedAt, sale.ExecutedAt, remaining,
                proceeds is { } p ? Round(p * share) : 0, 0, Round(sale.FeesEur * share), flags));
        }

        return new SaleResult(sale, lines, flags | lines.Aggregate(GainFlags.None, (all, l) => all | l.Flags));
    }

    private static decimal? ToEuro(decimal amount, string currency, DateTimeOffset when, RateTable rates, ISet<string> missingRates)
    {
        var date = DateOnly.FromDateTime(when.UtcDateTime);
        var result = rates.ToEuroExact(amount, currency, date);
        if (result is null)
            missingRates.Add($"{RateTable.Normalise(0, currency).Currency} {date:yyyy-MM-dd}");
        return result;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed class Lot(DateTimeOffset acquiredAt, decimal quantity, decimal? cost, decimal fees, bool missingRate)
    {
        public DateTimeOffset AcquiredAt { get; } = acquiredAt;
        public decimal Remaining { get; set; } = quantity;
        public decimal? Cost { get; set; } = cost;
        public decimal Fees { get; set; } = fees;
        public bool MissingRate { get; } = missingRate;
    }
}
