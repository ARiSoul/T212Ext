using Arisoul.T212Ext.Core.Domain;

namespace Arisoul.T212Ext.Core.Analytics;

/// <summary>One dividend, as the analytics need it. Amounts are net, in the analysis currency.</summary>
public sealed record DividendFact(DateTimeOffset PaidOn, decimal Amount, decimal Quantity, string Ticker, string? Name, string? Isin, string? InstrumentCurrency);

/// <summary>One cash movement that counts as passive income (interest, lending interest, dividend adjustments).</summary>
public sealed record IncomeFact(DateTimeOffset DateTime, decimal Amount, string Type);

/// <summary>A position from the latest snapshot.</summary>
public sealed record PositionFact(string Ticker, string? Name, string? Isin, string? InstrumentCurrency, decimal Quantity, decimal? CurrentValue, decimal? TotalCost, decimal? UnrealizedProfitLoss);

public sealed record SnapshotFact(DateOnly Date, decimal? TotalValue, decimal? TotalCost);

public sealed record MonthPoint(int Year, int Month, decimal Amount, int Count, decimal? MovingAverage12);

public sealed record YearPoint(int Year, decimal Amount, int Count, decimal? GrowthPercent, bool IsCurrentYear);

public sealed record InstrumentIncome(
    string Ticker, string? Name, string? Isin, string? Country, string? InstrumentCurrency,
    decimal Total, decimal Last12Months, int Count, DateTimeOffset FirstPaid, DateTimeOffset LastPaid, decimal ShareOfTotal, bool Held);

public sealed record Slice(string Key, decimal Amount, decimal Share);

public sealed record PassiveIncomeMonth(int Year, int Month, decimal Dividends, decimal Interest, decimal Lending);

public sealed record PositionIncome(
    string Ticker, string? Name, string? Isin, string? InstrumentCurrency, decimal Quantity,
    decimal? CurrentValue, decimal? TotalCost, decimal? UnrealizedProfitLoss,
    decimal Last12Months, decimal? YieldOnCost, decimal? CurrentYield, decimal ForwardEstimate);

public sealed record DividendDashboard(
    string Currency,
    decimal AllTime,
    int PaymentCount,
    decimal Last12Months,
    decimal Previous12Months,
    decimal? Last12MonthsGrowthPercent,
    decimal YearToDate,
    decimal SamePeriodLastYear,
    decimal? YearToDateGrowthPercent,
    decimal ForwardEstimate,
    decimal? PortfolioYieldOnCost,
    IReadOnlyList<MonthPoint> Months,
    IReadOnlyList<YearPoint> Years,
    IReadOnlyList<InstrumentIncome> Instruments,
    IReadOnlyList<Slice> Countries,
    IReadOnlyList<Slice> Currencies,
    IReadOnlyList<PassiveIncomeMonth> PassiveIncome,
    IReadOnlyList<PositionIncome> Positions,
    IReadOnlyList<SnapshotFact> Portfolio);

/// <summary>
/// The dividend figures of the dashboard, computed in memory from plain facts so they can be tested
/// without a database. Every amount is in one currency: callers never mix currencies.
/// </summary>
/// <remarks>
/// <para>"Last 12 months" is the 365 days before <c>now</c>. The forward estimate is naive on purpose: each
/// holding's net income per share over the last 12 months times the shares held today.</para>
/// <para>The country of an instrument is the first two letters of its ISIN (the country of issue).</para>
/// </remarks>
public static class DividendAnalytics
{
    public static DividendDashboard Compute(
        string currency,
        IReadOnlyList<DividendFact> dividends,
        IReadOnlyList<IncomeFact> otherIncome,
        IReadOnlyList<PositionFact> positions,
        IReadOnlyList<SnapshotFact> snapshots,
        DateTimeOffset now)
    {
        var yearAgo = now.AddYears(-1);
        var twoYearsAgo = now.AddYears(-2);
        var startOfYear = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var startOfLastYear = startOfYear.AddYears(-1);
        var sameDayLastYear = now.AddYears(-1);

        var last12 = dividends.Where(d => d.PaidOn > yearAgo && d.PaidOn <= now).Sum(d => d.Amount);
        var previous12 = dividends.Where(d => d.PaidOn > twoYearsAgo && d.PaidOn <= yearAgo).Sum(d => d.Amount);
        var ytd = dividends.Where(d => d.PaidOn >= startOfYear && d.PaidOn <= now).Sum(d => d.Amount);
        var samePeriodLastYear = dividends.Where(d => d.PaidOn >= startOfLastYear && d.PaidOn <= sameDayLastYear).Sum(d => d.Amount);

        var positionIncome = PositionsIncome(dividends, positions, yearAgo, now);
        var totalCost = positionIncome.Where(p => p.TotalCost is > 0).Sum(p => p.TotalCost!.Value);
        var heldLast12 = positionIncome.Sum(p => p.Last12Months);

        return new DividendDashboard(
            currency,
            dividends.Sum(d => d.Amount),
            dividends.Count,
            last12,
            previous12,
            Growth(last12, previous12),
            ytd,
            samePeriodLastYear,
            Growth(ytd, samePeriodLastYear),
            positionIncome.Sum(p => p.ForwardEstimate),
            totalCost > 0 ? heldLast12 / totalCost : null,
            Months(dividends, now),
            Years(dividends, now),
            Instruments(dividends, positions, yearAgo, now),
            Slices(dividends.GroupBy(d => CountryOf(d.Isin) ?? "?")),
            Slices(dividends.GroupBy(d => d.InstrumentCurrency ?? "?")),
            Passive(dividends, otherIncome, now),
            positionIncome,
            [.. snapshots.OrderBy(s => s.Date)]);
    }

    /// <summary>The country of issue from an ISIN ("US0378331005" → "US"); null when it is not an ISIN.</summary>
    public static string? CountryOf(string? isin) =>
        isin is { Length: >= 2 } && char.IsAsciiLetterUpper(isin[0]) && char.IsAsciiLetterUpper(isin[1]) ? isin[..2] : null;

    /// <summary>Percent change from <paramref name="previous"/> to <paramref name="current"/>; null when there is nothing to compare with.</summary>
    public static decimal? Growth(decimal current, decimal previous) =>
        previous > 0 ? Math.Round((current - previous) / previous * 100, 1) : null;

    internal static IReadOnlyList<MonthPoint> Months(IReadOnlyList<DividendFact> dividends, DateTimeOffset now)
    {
        if (dividends.Count == 0)
            return [];

        var byMonth = dividends
            .GroupBy(d => (d.PaidOn.UtcDateTime.Year, d.PaidOn.UtcDateTime.Month))
            .ToDictionary(g => g.Key, g => (Amount: g.Sum(d => d.Amount), Count: g.Count()));

        var first = dividends.Min(d => d.PaidOn).UtcDateTime;
        var cursor = new DateOnly(first.Year, first.Month, 1);
        var last = new DateOnly(now.UtcDateTime.Year, now.UtcDateTime.Month, 1);
        var points = new List<MonthPoint>();
        var window = new Queue<decimal>();

        while (cursor <= last)
        {
            var (amount, count) = byMonth.GetValueOrDefault((cursor.Year, cursor.Month));
            window.Enqueue(amount);
            if (window.Count > 12)
                window.Dequeue();

            points.Add(new MonthPoint(cursor.Year, cursor.Month, amount, count, window.Count == 12 ? Math.Round(window.Average(), 2) : null));
            cursor = cursor.AddMonths(1);
        }

        return points;
    }

    internal static IReadOnlyList<YearPoint> Years(IReadOnlyList<DividendFact> dividends, DateTimeOffset now)
    {
        var years = dividends
            .GroupBy(d => d.PaidOn.UtcDateTime.Year)
            .OrderBy(g => g.Key)
            .Select(g => (Year: g.Key, Amount: g.Sum(d => d.Amount), Count: g.Count()))
            .ToList();

        return [.. years.Select((y, i) => new YearPoint(
            y.Year,
            y.Amount,
            y.Count,
            i > 0 && years[i - 1].Year == y.Year - 1 ? Growth(y.Amount, years[i - 1].Amount) : null,
            y.Year == now.UtcDateTime.Year))];
    }

    private static IReadOnlyList<InstrumentIncome> Instruments(IReadOnlyList<DividendFact> dividends, IReadOnlyList<PositionFact> positions, DateTimeOffset yearAgo, DateTimeOffset now)
    {
        var total = dividends.Sum(d => d.Amount);
        return [.. dividends
            .GroupBy(InstrumentKey)
            .Select(g =>
            {
                var latest = g.MaxBy(d => d.PaidOn)!;
                var amount = g.Sum(d => d.Amount);
                return new InstrumentIncome(
                    latest.Ticker,
                    g.Select(d => d.Name).LastOrDefault(n => n is not null),
                    latest.Isin,
                    CountryOf(latest.Isin),
                    latest.InstrumentCurrency,
                    amount,
                    g.Where(d => d.PaidOn > yearAgo && d.PaidOn <= now).Sum(d => d.Amount),
                    g.Count(),
                    g.Min(d => d.PaidOn),
                    latest.PaidOn,
                    total > 0 ? amount / total : 0,
                    positions.Any(p => SameInstrument(p, latest)));
            })
            .OrderByDescending(i => i.Total)];
    }

    private static IReadOnlyList<PositionIncome> PositionsIncome(IReadOnlyList<DividendFact> dividends, IReadOnlyList<PositionFact> positions, DateTimeOffset yearAgo, DateTimeOffset now) =>
        [.. positions.Select(p =>
        {
            var recent = dividends.Where(d => SameInstrument(p, d) && d.PaidOn > yearAgo && d.PaidOn <= now).ToList();
            var last12 = recent.Sum(d => d.Amount);
            var perShare = recent.Where(d => d.Quantity > 0).Sum(d => d.Amount / d.Quantity);
            return new PositionIncome(
                p.Ticker, p.Name, p.Isin, p.InstrumentCurrency, p.Quantity, p.CurrentValue, p.TotalCost, p.UnrealizedProfitLoss,
                last12,
                p.TotalCost is > 0 ? last12 / p.TotalCost : null,
                p.CurrentValue is > 0 ? last12 / p.CurrentValue : null,
                Math.Round(perShare * p.Quantity, 2));
        })
        .OrderByDescending(p => p.CurrentValue ?? 0)];

    private static IReadOnlyList<PassiveIncomeMonth> Passive(IReadOnlyList<DividendFact> dividends, IReadOnlyList<IncomeFact> otherIncome, DateTimeOffset now)
    {
        var months = new List<PassiveIncomeMonth>();
        var start = new DateOnly(now.UtcDateTime.Year, now.UtcDateTime.Month, 1).AddMonths(-11);

        for (var m = start; m <= start.AddMonths(11); m = m.AddMonths(1))
        {
            bool InMonth(DateTimeOffset t) => t.UtcDateTime.Year == m.Year && t.UtcDateTime.Month == m.Month;
            var income = otherIncome.Where(i => InMonth(i.DateTime)).ToList();

            months.Add(new PassiveIncomeMonth(
                m.Year,
                m.Month,
                dividends.Where(d => InMonth(d.PaidOn)).Sum(d => d.Amount)
                    + income.Where(i => i.Type == CashTransactionTypes.DividendAdjustment).Sum(i => i.Amount),
                income.Where(i => i.Type == CashTransactionTypes.InterestOnFreeCash).Sum(i => i.Amount),
                income.Where(i => i.Type == CashTransactionTypes.LendingInterest).Sum(i => i.Amount)));
        }

        return months;
    }

    /// <summary>Largest first; at most six slices, the rest folded into "Other" (key "*").</summary>
    private static IReadOnlyList<Slice> Slices(IEnumerable<IGrouping<string, DividendFact>> groups)
    {
        var all = groups.Select(g => (g.Key, Amount: g.Sum(d => d.Amount))).OrderByDescending(s => s.Amount).ToList();
        var total = all.Sum(s => s.Amount);
        if (total <= 0)
            return [];

        var shown = all.Count <= 6 ? all : [.. all.Take(5), ("*", all.Skip(5).Sum(s => s.Amount))];
        return [.. shown.Select(s => new Slice(s.Key, s.Amount, s.Amount / total))];
    }

    private static string InstrumentKey(DividendFact d) => d.Isin ?? d.Ticker;

    private static bool SameInstrument(PositionFact position, DividendFact dividend) =>
        !string.IsNullOrEmpty(position.Isin) && !string.IsNullOrEmpty(dividend.Isin)
            ? string.Equals(position.Isin, dividend.Isin, StringComparison.OrdinalIgnoreCase)
            : string.Equals(position.Ticker, dividend.Ticker, StringComparison.OrdinalIgnoreCase);
}
