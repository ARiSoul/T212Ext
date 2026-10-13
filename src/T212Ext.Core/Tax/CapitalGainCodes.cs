namespace Arisoul.T212Ext.Core.Tax;

/// <summary>The Anexo J, quadro 9.2A, code of a sale by the instrument's type (see docs/TAX.md).</summary>
public static class CapitalGainCodes
{
    public const string Shares = "G01";
    public const string FundUnits = "G20";

    /// <summary>
    /// ETFs are fund units (G20, the code whose wording fits best; G90 and G01 are also seen, and are
    /// taxed alike: to confirm, see TAX.md). Everything else is treated as shares (G01).
    /// </summary>
    public static string ForInstrumentType(string? type) =>
        string.Equals(type, "ETF", StringComparison.OrdinalIgnoreCase) ? FundUnits : Shares;
}
