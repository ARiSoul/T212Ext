namespace Arisoul.T212Ext.Core.Domain;

/// <summary>
/// ISINs, currency and country codes are stored in upper case, trimmed: PostgreSQL compares text
/// case-sensitively, so every place a code comes in (the API, CSV files, forms, addresses) writes it
/// one way (docs/CONVENTIONS.md, 3.15). Tickers keep their case: Trading 212's own ("VUSAl_EQ") are
/// mixed case and always come from it.
/// </summary>
public static class Codes
{
    /// <summary>Null for an empty value.</summary>
    public static string? Upper(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}
