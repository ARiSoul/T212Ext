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

    private const string ManufacturedSuffix = "_MANUFACTURED_PAYMENT";

    /// <summary>
    /// The instrument's public page at Trading 212, for the ticker forms whose address is known:
    /// "AAPL_US_EQ" → …/invest/AAPL.US and London's "VUSAl_EQ" → …/invest/VUSA.GB. Null for the rest
    /// (other exchanges' addresses are not known: no link rather than a wrong one). See T212-API.md.
    /// </summary>
    public static string? TradingPage(string? ticker)
    {
        if (ticker is null)
            return null;

        string? symbol = null, market = null;
        if (ticker.EndsWith("_US_EQ", StringComparison.Ordinal))
            (symbol, market) = (ticker[..^"_US_EQ".Length], "US");
        else if (ticker.EndsWith("l_EQ", StringComparison.Ordinal))
            (symbol, market) = (ticker[..^"l_EQ".Length], "GB");

        return symbol is { Length: > 0 and <= 12 } && symbol.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '.')
            ? $"https://www.trading212.com/trading-instruments/invest/{symbol}.{market}"
            : null;
    }

    /// <summary>
    /// A dividend's type, translated. The API sends codes ("DIVIDENDS_PAID_BY_US_CORPORATIONS"), the CSV
    /// reports the same words ("Dividends paid by us corporations"): both become one key. A
    /// "…_MANUFACTURED_PAYMENT" (paid instead of the dividend while the shares are lent) is its base type
    /// with a note. A type without a translation is humanised, so new ones still read well.
    /// </summary>
    public static string DividendType(IStringLocalizer<SharedResource> l, string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return "—";

        var key = KeyOf(type);
        var manufactured = key.EndsWith(ManufacturedSuffix, StringComparison.Ordinal) && key.Length > ManufacturedSuffix.Length;
        var baseKey = manufactured ? key[..^ManufacturedSuffix.Length] : key;

        var text = l[$"DividendType_{baseKey}"];
        var name = text.ResourceNotFound ? Humanize(manufactured ? baseKey : type) : text.Value;
        return manufactured ? string.Format(CultureInfo.CurrentCulture, l["DividendType_Manufactured"], name) : name;
    }

    /// <summary>"Dividends paid by us corporations" and "DIVIDENDS_PAID_BY_US_CORPORATIONS" → the same key.</summary>
    public static string KeyOf(string code)
    {
        var builder = new System.Text.StringBuilder(code.Length);
        foreach (var c in code.Trim().ToUpperInvariant())
            builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        var key = builder.ToString();
        while (key.Contains("__", StringComparison.Ordinal))
            key = key.Replace("__", "_", StringComparison.Ordinal);
        return key.Trim('_');
    }

    /// <summary>"DIVIDENDS_PAID_BY_US_CORPORATIONS" → "Dividends paid by us corporations". Codes Trading 212 adds keep working.</summary>
    public static string Humanize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "—";

        var text = code.Replace('_', ' ').Trim().ToLowerInvariant();
        return char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
    }
}
