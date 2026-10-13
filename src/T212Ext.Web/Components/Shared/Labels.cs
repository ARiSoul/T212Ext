using System.Globalization;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Web.Resources;
using Microsoft.Extensions.Localization;

namespace Arisoul.T212Ext.Web.Components.Shared;

/// <summary>Names of codes that come from Trading 212 or the database, in the current language.</summary>
public static class Labels
{
    /// <summary>Cash movement types have translated names; an unknown type falls back to <see cref="Humanize"/>.</summary>
    public static string CashType(IStringLocalizer<SharedResource> l, string type)
    {
        var text = l[$"CashType_{type}"];
        return text.ResourceNotFound ? Humanize(type) : text;
    }

    public static string Side(IStringLocalizer<SharedResource> l, string? side) =>
        side switch
        {
            "BUY" => l["Order_Buy"],
            "SELL" => l["Order_Sell"],
            null => "—",
            _ => Humanize(side),
        };

    public static string Job(IStringLocalizer<SharedResource> l, SyncJob job) => l[$"SyncJob_{job}"];

    /// <summary>The countries with a translated name (the usual ISIN countries), for pickers.</summary>
    public static IReadOnlyList<string> KnownCountries { get; } =
    [
        "US", "IE", "GB", "DE", "FR", "NL", "CH", "LU", "BE", "ES", "PT", "IT", "AT", "DK", "SE", "NO", "FI",
        "CA", "AU", "JP", "HK", "CN", "SG", "CW", "JE", "GG", "IM", "BM", "KY", "VG", "PA", "BR", "IL", "CY", "BG",
    ];

    /// <summary>A country's name from its ISO code: translated for the usual ISIN countries, else the English name, else the code.</summary>
    public static string Country(IStringLocalizer<SharedResource> l, string? code)
    {
        if (string.IsNullOrEmpty(code) || code == "?")
            return "—";

        var text = l[$"Country_{code}"];
        if (!text.ResourceNotFound)
            return text;

        try
        {
            return new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return code;
        }
    }

    public static string Sources(IStringLocalizer<SharedResource> l, DataSources sources) =>
        sources switch
        {
            DataSources.Api | DataSources.Csv => l["Source_Both"],
            DataSources.Csv => l["Source_Csv"],
            _ => l["Source_Api"],
        };

    /// <summary>"DIVIDENDS_PAID_BY_US_CORPORATIONS" → "Dividends paid by us corporations". Codes Trading 212 adds keep working.</summary>
    public static string Humanize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "—";

        var text = code.Replace('_', ' ').Trim().ToLowerInvariant();
        return char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
    }
}
