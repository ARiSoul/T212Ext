namespace Arisoul.T212Ext.Core.Rates;

/// <summary>
/// Converts amounts to euros with the stored ECB rates. The rate of a day without one (weekend,
/// holiday) is the last rate before it, up to <see cref="MaxGapDays"/> days back.
/// </summary>
public sealed class RateTable
{
    public const int MaxGapDays = 7;

    private readonly Dictionary<string, SortedList<DateOnly, decimal>> _rates;

    public RateTable(IEnumerable<(string Currency, DateOnly Date, decimal PerEuro)> rates) =>
        _rates = rates
            .GroupBy(r => r.Currency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new SortedList<DateOnly, decimal>(g.ToDictionary(r => r.Date, r => r.PerEuro)), StringComparer.OrdinalIgnoreCase);

    /// <summary>The rate used for <paramref name="date"/> and the date it is from; null when there is none close enough.</summary>
    public (DateOnly Date, decimal PerEuro)? RateFor(string currency, DateOnly date)
    {
        if (string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase))
            return (date, 1m);

        if (!_rates.TryGetValue(currency, out var rates) || rates.Count == 0)
            return null;

        // Binary search for the last date on or before the one asked.
        var keys = rates.Keys;
        int low = 0, high = keys.Count - 1, found = -1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (keys[mid] <= date)
            {
                found = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (found < 0 || date.DayNumber - keys[found].DayNumber > MaxGapDays)
            return null;

        return (keys[found], rates.Values[found]);
    }

    /// <summary>The amount in euros, or null without a rate. Pence (GBX) count as 1/100 of a pound.</summary>
    public decimal? ToEuro(decimal amount, string? currency, DateOnly date)
    {
        if (string.IsNullOrEmpty(currency))
            return null;

        (amount, currency) = Normalise(amount, currency);
        return RateFor(currency, date) is { } rate ? Math.Round(amount / rate.PerEuro, 2, MidpointRounding.AwayFromZero) : null;
    }

    /// <summary>The ECB has no rate for pence: GBX (or GBp) amounts are pounds divided by 100.</summary>
    public static (decimal Amount, string Currency) Normalise(decimal amount, string currency) =>
        currency is "GBX" or "GBp" or "gbx" ? (amount / 100, "GBP") : (amount, currency.ToUpperInvariant());
}
