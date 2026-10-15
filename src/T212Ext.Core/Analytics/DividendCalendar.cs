namespace Arisoul.T212Ext.Core.Analytics;

public enum ExpectedStatus
{
    /// <summary>Expected ahead, from last year's payment.</summary>
    Estimated,

    /// <summary>Its date passed by less than the usual slip: any day now.</summary>
    Due,

    /// <summary>Its date passed by more than the usual slip and it was not paid: late, cut or suspended.</summary>
    Late,
}

/// <summary>
/// A payment expected from a holding. <see cref="PerShare"/> and <see cref="Amount"/> are net, in the
/// analysis currency; <see cref="BasedOn"/> is the paid date it was projected from.
/// </summary>
public sealed record ExpectedPayment(
    string Ticker,
    string? Name,
    string? Isin,
    string? InstrumentCurrency,
    DateOnly ExpectedOn,
    decimal PerShare,
    decimal Quantity,
    decimal Amount,
    DateOnly BasedOn,
    ExpectedStatus Status);

/// <summary>
/// The dividends the holdings are expected to pay in the next 12 months, projected from their own
/// history: Trading 212 has no upcoming dividends, and the providers that do are paid.
/// </summary>
/// <remarks>
/// <para>Each payment of the last 12 months is expected again a year later, unless it was already
/// paid again (within a tolerance). That keeps each slot apart, so a smaller interim and a larger
/// final dividend are both right, and it needs no frequency to be guessed.</para>
/// <para>The amount per share is last year's for that slot; for holdings that pay three or more times
/// a year (slots of equal size) it is the latest one, so raises show at once. Times the shares held today.</para>
/// <para>A holding with under a year of history has holes in last year's slots: the usual gap after the
/// last payment fills them in, at the last amount per share.</para>
/// <para>The next 12 months are the <see cref="ExpectedStatus.Estimated"/> payments; due and late ones are
/// shown apart, so a payment a few days late is not counted twice (now and a year later).</para>
/// <para>Left out: bonus and capital gains distributions, and, for regular payers, a payment over twice
/// the usual (a special dividend). A holding that has not paid for over 13 months is not expected to;
/// one that is a payment late shows that payment as <see cref="ExpectedStatus.Late"/> and nothing after it.</para>
/// </remarks>
public static class DividendCalendar
{
    private static readonly string[] Irregular = ["BONUS", "CAPITAL_GAINS", "DEMERGER", "INTERIM_LIQUIDATION"];

    /// <summary>A payment over this times the usual one, from a holding that pays three or more times a year, is special.</summary>
    public const decimal SpecialFactor = 2m;

    /// <summary>"Long term capital gains" (CSV) and "LONG_TERM_CAPITAL_GAINS" (API) are both irregular.</summary>
    public static bool IsRegular(string? type)
    {
        if (type is null)
            return true;
        var key = new string([.. type.ToUpperInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')]);
        return !Irregular.Any(i => key.Contains(i, StringComparison.Ordinal));
    }

    public static IReadOnlyList<ExpectedPayment> Project(IReadOnlyList<DividendFact> dividends, IReadOnlyList<PositionFact> positions, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var expected = new List<ExpectedPayment>();

        foreach (var holding in positions.GroupBy(p => p.Isin ?? p.Ticker, StringComparer.OrdinalIgnoreCase))
        {
            var quantity = holding.Sum(p => p.Quantity);
            var position = holding.First();
            if (quantity <= 0)
                continue;

            // One payment per date. Rows of one type from several accounts share the amount per share;
            // rows of several types (ordinary and return of capital) add up.
            var payments = dividends
                .Where(d => DividendAnalytics.SameInstrument(position, d) && d.Quantity > 0 && IsRegular(d.Type))
                .GroupBy(d => DateOnly.FromDateTime(d.PaidOn.UtcDateTime))
                .Select(g => new Payment(g.Key, g.GroupBy(d => d.Type).Sum(t => t.Sum(d => d.Amount) / t.Sum(d => d.Quantity)), g.Last()))
                .Where(p => p.Date <= today && p.Date > today.AddYears(-2))
                .OrderBy(p => p.Date)
                .ToList();
            if (payments.Count == 0)
                continue;

            var gap = MedianGap(payments);
            var tolerance = Math.Clamp(gap / 2, 10, 45);
            var regular = payments.Count(p => p.Date > today.AddYears(-1)) >= 3;
            if (regular)
            {
                var usual = Median(payments.Select(p => p.PerShare));
                payments.RemoveAll(p => p.PerShare > usual * SpecialFactor);
                if (payments.Count == 0)
                    continue;
            }

            var last = payments[^1];
            if (last.Date < today.AddMonths(-13))
                continue;

            ExpectedPayment Expect(DateOnly on, Payment from, ExpectedStatus status)
            {
                var perShare = regular ? last.PerShare : from.PerShare;
                return new ExpectedPayment(position.Ticker, position.Name ?? from.Fact.Name, position.Isin ?? from.Fact.Isin, position.InstrumentCurrency ?? from.Fact.InstrumentCurrency,
                    on, Math.Round(perShare, 6), quantity, Math.Round(perShare * quantity, 2), from.Date, status);
            }

            // A payment late: shown once, and nothing expected after it.
            if (today.DayNumber - last.Date.DayNumber > gap + tolerance)
            {
                expected.Add(Expect(last.Date.AddDays(gap), last, ExpectedStatus.Late));
                continue;
            }

            var dates = payments.Select(p => p.Date).ToList();
            bool Covered(DateOnly on) => dates.Any(d => Math.Abs(d.DayNumber - on.DayNumber) <= tolerance);
            void Add(DateOnly on, Payment from)
            {
                dates.Add(on);
                expected.Add(Expect(on, from, on < today ? ExpectedStatus.Due : ExpectedStatus.Estimated));
            }

            foreach (var slot in payments.Where(p => p.Date > today.AddYears(-1).AddDays(-tolerance)))
            {
                var on = slot.Date.AddYears(1);
                if (!Covered(on))   // otherwise paid again already
                    Add(on, slot);
            }

            // Under a year of history (a new holding): last year's slots are not all there, so the usual gap fills in.
            if (payments[0].Date > today.AddYears(-1))
            {
                for (var on = last.Date.AddDays(gap); on <= today.AddYears(1); on = on.AddDays(gap))
                {
                    if (!Covered(on))
                        Add(on, last);
                }
            }
        }

        return [.. expected.OrderBy(e => e.ExpectedOn).ThenBy(e => e.Name ?? e.Ticker)];
    }

    private sealed record Payment(DateOnly Date, decimal PerShare, DividendFact Fact);

    /// <summary>Days between payments; a year when there is only one.</summary>
    private static int MedianGap(List<Payment> payments) =>
        payments.Count < 2 ? 365 : (int)Median(payments.Zip(payments.Skip(1), (a, b) => (decimal)(b.Date.DayNumber - a.Date.DayNumber)));

    private static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
