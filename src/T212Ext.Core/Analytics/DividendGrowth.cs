namespace Arisoul.T212Ext.Core.Analytics;

/// <summary>One payment's dividend per share, in the instrument's currency (gross when known).</summary>
public sealed record PerShareFact(string Ticker, string? Name, string? Isin, string? Currency, DateOnly PaidOn, decimal PerShare, string Type);

/// <summary>A year's dividend per share. <see cref="Growth"/> is against the year before, when the two compare.</summary>
public sealed record DividendYear(int Year, decimal PerShare, int Payments, decimal? Growth, bool Partial);

/// <summary>How an instrument's dividend per share moved, year by year.</summary>
public sealed record DividendGrowth(
    string Ticker,
    string? Name,
    string? Isin,
    string? Currency,
    IReadOnlyList<DividendYear> Years,
    decimal? LastGrowth,
    int GrowthStreak,
    decimal? Cagr,
    int CagrYears)
{
    /// <summary>The last full year paid at least 1 % less per share than the one before.</summary>
    public bool Cut => LastGrowth < -DividendGrowthAnalysis.Tolerance;

    public string Key => DividendGrowthAnalysis.KeyOf(Ticker, Isin);
}

/// <summary>
/// Dividend growth per instrument, from the dividend per share in its own currency, so neither the
/// shares held nor the exchange rate move it. Two years compare only when they had the same number
/// of payments: a quarterly payment that slips into January would otherwise look like a cut followed
/// by a rise. The current year is shown but never compared. Returns of capital, demergers and
/// liquidations are not dividends and are left out.
/// </summary>
public static class DividendGrowthAnalysis
{
    /// <summary>Moves smaller than this (1 %) are rounding, not growth or a cut.</summary>
    public const decimal Tolerance = 0.01m;

    private static readonly string[] NotDividends = ["RETURN_OF_CAPITAL", "DEMERGER", "INTERIM_LIQUIDATION"];

    public static string KeyOf(string ticker, string? isin) => isin ?? ticker;

    /// <summary>"Return of capital" (CSV) and "RETURN_OF_CAPITAL_NON_US" (API) both count as not a dividend.</summary>
    public static bool IsDividend(string type)
    {
        var key = new string([.. type.ToUpperInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')]);
        return !NotDividends.Any(n => key.Contains(n, StringComparison.Ordinal));
    }

    public static IReadOnlyList<DividendGrowth> Compute(IEnumerable<PerShareFact> facts, int currentYear) =>
        [.. facts
            .Where(f => f.PerShare > 0 && IsDividend(f.Type))
            .GroupBy(f => KeyOf(f.Ticker, f.Isin))
            .Select(g => Instrument(g.ToList(), currentYear))];

    private static DividendGrowth Instrument(List<PerShareFact> facts, int currentYear)
    {
        var latest = facts.MaxBy(f => f.PaidOn)!;

        // One payment per date: the same dividend can come from several accounts.
        var payments = facts.GroupBy(f => f.PaidOn).Select(g => (Date: g.Key, PerShare: g.Max(f => f.PerShare)));
        var byYear = payments.GroupBy(p => p.Date.Year)
            .OrderBy(g => g.Key)
            .Select(g => (Year: g.Key, PerShare: g.Sum(p => p.PerShare), Payments: g.Count()))
            .ToList();

        var years = new List<DividendYear>();
        for (var i = 0; i < byYear.Count; i++)
        {
            var (year, perShare, count) = byYear[i];
            var partial = year >= currentYear;
            decimal? growth = null;
            if (!partial && i > 0 && byYear[i - 1] is var previous && previous.Year == year - 1 && previous.Payments == count && previous.PerShare > 0)
                growth = Math.Round(perShare / previous.PerShare - 1, 4);
            years.Add(new DividendYear(year, perShare, count, growth, partial));
        }

        var lastFull = years.LastOrDefault(y => y.Year == currentYear - 1);
        var streak = 0;
        if (lastFull is not null)
        {
            for (var i = years.IndexOf(lastFull); i >= 0 && years[i].Growth > Tolerance; i--)
                streak++;
        }

        // Compound growth over the longest run of comparable years that ends with the last full one.
        decimal? cagr = null;
        var cagrYears = 0;
        if (lastFull is not null)
        {
            var start = years.IndexOf(lastFull);
            while (start > 0 && years[start].Growth is not null)
                start--;
            cagrYears = lastFull.Year - years[start].Year;
            if (cagrYears >= 1 && years[start].PerShare > 0)
                cagr = Math.Round((decimal)Math.Pow((double)(lastFull.PerShare / years[start].PerShare), 1.0 / cagrYears) - 1, 4);
        }

        return new DividendGrowth(latest.Ticker, latest.Name, latest.Isin, latest.Currency, years, lastFull?.Growth, streak, cagr, cagr is null ? 0 : cagrYears);
    }
}
