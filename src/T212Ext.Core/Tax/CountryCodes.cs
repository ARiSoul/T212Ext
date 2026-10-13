namespace Arisoul.T212Ext.Core.Tax;

/// <summary>
/// ISO 3166 numeric codes, which the IRS forms use for countries (Anexo J, "Tabela X"), for the
/// countries an ISIN usually comes from. Add a country here when an instrument needs it.
/// </summary>
public static class CountryCodes
{
    private static readonly Dictionary<string, int> Codes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AT"] = 40,
        ["AU"] = 36,
        ["BE"] = 56,
        ["BG"] = 100,
        ["BM"] = 60,
        ["BR"] = 76,
        ["CA"] = 124,
        ["CH"] = 756,
        ["CN"] = 156,
        ["CY"] = 196,
        ["DE"] = 276,
        ["DK"] = 208,
        ["ES"] = 724,
        ["FI"] = 246,
        ["FR"] = 250,
        ["GB"] = 826,
        ["GG"] = 831,
        ["HK"] = 344,
        ["IE"] = 372,
        ["IL"] = 376,
        ["IM"] = 833,
        ["IT"] = 380,
        ["JE"] = 832,
        ["JP"] = 392,
        ["KY"] = 136,
        ["LU"] = 442,
        ["NL"] = 528,
        ["NO"] = 578,
        ["PA"] = 591,
        ["PT"] = 620,
        ["SE"] = 752,
        ["SG"] = 702,
        ["US"] = 840,
        ["VG"] = 92,
    };

    /// <summary>The numeric code of an ISO alpha-2 country, or null when unknown (or not a country, like "XS").</summary>
    public static int? Numeric(string? alpha2) =>
        alpha2 is not null && Codes.TryGetValue(alpha2, out var code) ? code : null;

    /// <summary>"840", "036": three digits, as the form shows them.</summary>
    public static string Format(int? code) => code is null ? "—" : code.Value.ToString("000");
}
