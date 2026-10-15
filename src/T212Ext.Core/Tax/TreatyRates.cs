namespace Arisoul.T212Ext.Core.Tax;

/// <summary>
/// What a double taxation treaty lets the source country withhold on portfolio dividends paid to a
/// Portuguese resident. <see cref="Rate"/> is null when there is no treaty in force: the credit is
/// then limited only by the Portuguese tax on that income (28 %, Código do IRS art. 81.º n.º 1).
/// </summary>
public sealed record TreatyLimit(decimal? Rate, string Note)
{
    /// <summary>The highest rate of foreign tax that can be credited in Portugal.</summary>
    public decimal CreditRate => Rate ?? IrsReport.PortugueseRate;
}

/// <summary>
/// Treaty dividend rates for the countries a Trading 212 portfolio usually holds. Source: AT, "Tabela
/// prática das CDT" (2026), and the treaties it cites; see docs/TAX.md. A country not listed is not
/// known: no limit is applied and the usual 15 % is only used for the warning.
/// </summary>
public static class TreatyRates
{
    private sealed record Period(int From, int To, decimal? Rate, string Note);

    private static readonly IReadOnlyDictionary<string, Period[]> Table = new Dictionary<string, Period[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        ["IE"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        // The 2025 treaty applies to withholding from 1 January 2026 (Aviso 1/2026/1); before, the 1968 one.
        ["GB"] = [new(0, 2025, 0.15m, "art. 10.º (1968)"), new(2026, int.MaxValue, 0.10m, "art. 10.º (2025)")],
        ["NL"] = [new(0, int.MaxValue, 0.10m, "art. 10.º")],
        ["DE"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        ["FR"] = [new(0, int.MaxValue, 0.15m, "art. 11.º")],
        ["CH"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        ["ES"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        ["IT"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        ["CA"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        ["LU"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        ["BE"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        ["DK"] = [new(0, int.MaxValue, 0.10m, "art. 10.º")],
        ["NO"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        ["JP"] = [new(0, int.MaxValue, 0.10m, "art. 10.º")],
        ["AT"] = [new(0, int.MaxValue, 0.15m, "art. 10.º")],
        // Terminated: Sweden from 2022 (Aviso 2/2022), Finland from 2019 (Aviso 146/2018).
        ["SE"] = [new(2022, int.MaxValue, null, "no treaty since 2022")],
        ["FI"] = [new(2019, int.MaxValue, null, "no treaty since 2019")],
        // Signed in 2023, not in force.
        ["AU"] = [new(0, int.MaxValue, null, "no treaty in force")],
    };

    /// <summary>The limit for dividends from <paramref name="country"/> paid in <paramref name="year"/>; null when not known.</summary>
    public static TreatyLimit? For(string? country, int year) =>
        country is not null && Table.TryGetValue(country, out var periods)
        && periods.FirstOrDefault(p => year >= p.From && year <= p.To) is { } period
            ? new TreatyLimit(period.Rate, period.Note)
            : null;
}
