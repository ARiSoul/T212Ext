using System.Text.RegularExpressions;
using Arisoul.T212Ext.Core.Analytics;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Rates;

namespace Arisoul.T212Ext.Core.Tax;

/// <summary>What may need a look before the figures of a dividend are used.</summary>
[Flags]
public enum IrsFlags
{
    None = 0,

    /// <summary>No withholding tax from a CSV report: it is estimated as gross − net.</summary>
    WithholdingEstimated = 1,

    /// <summary>No gross from the API: the gross is net + withholding (rounded to cents in CSV reports).</summary>
    GrossDerived = 2,

    /// <summary>Gross − withholding differs from the net received by more than the tolerance.</summary>
    Mismatch = 4,

    /// <summary>No exchange rate for the payment date: the amounts in euros are missing.</summary>
    MissingRate = 8,

    /// <summary>A return of capital: not income, left out of the lines.</summary>
    ReturnOfCapital = 16,

    /// <summary>No country of origin (no ISIN, or an international one like XS): set it on the instrument.</summary>
    NoCountry = 32,

    /// <summary>Withholding above the treaty rate (<see cref="TreatyRates"/>): the credit is capped at that rate.</summary>
    HighWithholding = 64,
}

/// <summary>One dividend, as the report needs it (amounts in their own currencies).</summary>
/// <param name="GrossFromApi">True when <see cref="GrossPerShare"/> came from the API (exact).</param>
/// <param name="NetPerShare">The CSV report's net per share, when imported.</param>
public sealed record IrsDividendInput(
    long Id,
    DateTimeOffset PaidOn,
    string Ticker,
    string? Name,
    string? Isin,
    string Type,
    decimal Quantity,
    decimal? GrossPerShare,
    string? InstrumentCurrency,
    decimal Net,
    string NetCurrency,
    decimal? WithholdingTax,
    string? WithholdingTaxCurrency,
    bool GrossFromApi = true,
    decimal? NetPerShare = null);

/// <summary>One cash movement that matters to the report: interest, share lending, withholding refunds.</summary>
public sealed record IrsCashInput(DateTimeOffset DateTime, string Type, decimal Amount, string Currency, string? Note);

public sealed record IrsDividend(
    IrsDividendInput Source,
    string? Country,
    decimal? GrossOriginal,
    DateOnly? RateDate,
    decimal? Rate,
    decimal? GrossEur,
    decimal? WithheldEur,
    decimal? NetEur,
    decimal? EffectiveRate,
    IrsFlags Flags,
    TreatyLimit? Treaty,
    decimal? CreditCapEur);

/// <summary>
/// One line of Anexo J, quadro 8A: income code and source country, with totals in euros.
/// <see cref="TaxPaidAbroad"/> is what to declare: <see cref="Withheld"/> (less refunds), capped at the
/// treaty rate when that is asked for.
/// </summary>
public sealed record IrsLine(
    int Number,
    string Code,
    string? Country,
    int? CountryNumeric,
    decimal Gross,
    decimal TaxPaidAbroad,
    decimal RefundsDeducted,
    int Count,
    decimal Withheld,
    TreatyLimit? Treaty);

/// <summary>
/// Interest and share lending, with their code and country. The code of interest on cash (E21) is
/// confirmed by the Anexo J instructions; share lending could be E21 or E22 (<see cref="CodeConfirmed"/> false).
/// </summary>
public sealed record IrsOtherIncome(string Type, string SuggestedCode, string? Country, int? CountryNumeric, decimal AmountEur, int Count, bool CodeConfirmed);

/// <summary>A withholding refund ("Dividend adjustment") and the tax year it refers to, from its note.</summary>
public sealed record IrsRefund(DateTimeOffset ReceivedOn, decimal AmountEur, int? ForYear, string? Country, string? Note);

/// <summary>Dividends from Portuguese companies: not foreign income, so not in Anexo J.</summary>
public sealed record IrsDomestic(int Count, decimal GrossEur, decimal WithheldEur);

public sealed record IrsReportResult(
    int Year,
    IReadOnlyList<IrsLine> Lines,
    IReadOnlyList<IrsDividend> Dividends,
    IReadOnlyList<IrsOtherIncome> OtherIncome,
    IReadOnlyList<IrsRefund> Refunds,
    IReadOnlyList<string> MissingRates,
    IrsDomestic Domestic)
{
    public decimal TotalGross => Lines.Sum(l => l.Gross);
    public decimal TotalTaxPaidAbroad => Lines.Sum(l => l.TaxPaidAbroad);
    public int FlaggedCount => Dividends.Count(d => d.Flags is not IrsFlags.None and not IrsFlags.HighWithholding);
}

/// <summary>
/// The figures for Anexo J, quadro 8A, of one tax year: foreign dividends (code E11, "sem retenção em
/// Portugal") by source country, gross, and tax paid abroad, in euros.
/// </summary>
/// <remarks>
/// <para>Rules, from the Código do IRS and AT guidance (see docs/TAX.md):</para>
/// <list type="bullet">
/// <item>Amounts in euros at the rate of the payment date, or of the last earlier quote (art. 23.º). ECB reference rates.</item>
/// <item>Gross = shares × gross dividend per share. Tax paid abroad = the withholding tax of the CSV report.</item>
/// <item>One line per code and country. The country is the source-country override of the instrument, else the ISIN's.</item>
/// <item>Withholding refunds for this year (by their note) are deducted from the tax paid in that country when asked.</item>
/// </list>
/// This prepares figures to check, it is not tax advice.
/// </remarks>
public static partial class IrsReport
{
    public const string DividendCode = "E11";
    public const string InterestCode = "E21";

    /// <summary>Portugal: its dividends are domestic income, not part of Anexo J.</summary>
    public const string DomesticCountry = "PT";

    /// <summary>The special rate on capital income (art. 72.º): the credit can never exceed it (art. 81.º n.º 1).</summary>
    public const decimal PortugueseRate = 0.28m;

    /// <summary>For a country whose treaty is not in <see cref="TreatyRates"/>: the usual treaty rate.</summary>
    public const decimal UsualTreatyRate = 0.15m;

    /// <summary>Withholding this much above the creditable rate is flagged (rounding stays below it).</summary>
    public const decimal WarningMargin = 0.005m;

    /// <summary>Gross − withheld may differ from net by FX fees and rounding: 2 % or 5 cents.</summary>
    private const decimal MismatchShare = 0.02m;
    private const decimal MismatchFloor = 0.05m;

    public static IrsReportResult Build(
        int year,
        IReadOnlyList<IrsDividendInput> dividends,
        IReadOnlyList<IrsCashInput> cash,
        IReadOnlyDictionary<string, string> sourceCountryByIsin,
        RateTable rates,
        string? interestCountry,
        bool deductRefunds,
        bool capAtTreaty = true)
    {
        var missingRates = new SortedSet<string>(StringComparer.Ordinal);
        var computed = dividends
            .Where(d => d.PaidOn.UtcDateTime.Year == year)
            .OrderBy(d => d.PaidOn)
            .Select(d => Compute(d, sourceCountryByIsin, rates, missingRates))
            .ToList();

        var refunds = cash
            .Where(c => c.Type == CashTransactionTypes.DividendAdjustment)
            .Select(c => new IrsRefund(c.DateTime, ToEuro(c.Amount, c.Currency, c.DateTime, rates, missingRates) ?? 0, YearOf(c.Note), CountryOfNote(c.Note), c.Note))
            .Where(r => r.ForYear == year)
            .ToList();

        var income = computed.Where(d => (d.Flags & IrsFlags.ReturnOfCapital) == 0 && d.GrossEur is not null).ToList();
        var domestic = income.Where(d => d.Country == DomesticCountry).ToList();

        var lines = income
            .Where(d => d.Country != DomesticCountry)
            .GroupBy(d => d.Country)
            .OrderByDescending(g => g.Sum(d => d.GrossEur!.Value))
            .Select((g, i) =>
            {
                var refunded = deductRefunds ? refunds.Where(r => r.Country == g.Key).Sum(r => r.AmountEur) : 0;
                var withheld = g.Sum(d => d.WithheldEur ?? 0);
                var paid = Math.Max(0, withheld - refunded);
                // Art. 81.º n.º 2: no more than the treaty allows is credited; the excess is reclaimed
                // from the source country. A dividend from a country not in the table keeps its tax.
                var cap = g.Sum(d => d.CreditCapEur ?? d.WithheldEur ?? 0);
                return new IrsLine(
                    801 + i,
                    DividendCode,
                    g.Key,
                    CountryCodes.Numeric(g.Key),
                    g.Sum(d => d.GrossEur!.Value),
                    capAtTreaty ? Math.Min(paid, cap) : paid,
                    Math.Min(refunded, withheld),
                    g.Count(),
                    paid,
                    g.First().Treaty);
            })
            .ToList();

        var other = cash
            .Where(c => c.DateTime.UtcDateTime.Year == year
                && c.Type is CashTransactionTypes.InterestOnFreeCash or CashTransactionTypes.LendingInterest)
            .GroupBy(c => c.Type)
            .Select(g => new IrsOtherIncome(
                g.Key,
                InterestCode,
                interestCountry,
                CountryCodes.Numeric(interestCountry),
                g.Sum(c => ToEuro(c.Amount, c.Currency, c.DateTime, rates, missingRates) ?? 0),
                g.Count(),
                g.Key == CashTransactionTypes.InterestOnFreeCash))
            .ToList();

        return new IrsReportResult(year, lines, computed, other, refunds, [.. missingRates],
            new IrsDomestic(domestic.Count, domestic.Sum(d => d.GrossEur!.Value), domestic.Sum(d => d.WithheldEur ?? 0)));
    }

    private static IrsDividend Compute(IrsDividendInput d, IReadOnlyDictionary<string, string> overrides, RateTable rates, ISet<string> missingRates)
    {
        var flags = IrsFlags.None;
        var date = DateOnly.FromDateTime(d.PaidOn.UtcDateTime);

        var country = d.Isin is not null && overrides.TryGetValue(d.Isin, out var chosen) ? chosen : DividendAnalytics.CountryOf(d.Isin);
        if (country is null || CountryCodes.Numeric(country) is null)
            flags |= IrsFlags.NoCountry;

        if (d.Type.Contains("RETURN_OF_CAPITAL", StringComparison.OrdinalIgnoreCase)
            || d.Type.Contains("Return of capital", StringComparison.OrdinalIgnoreCase))
            flags |= IrsFlags.ReturnOfCapital;

        var netEur = ToEuro(d.Net, d.NetCurrency, d.PaidOn, rates, missingRates);
        decimal? grossOriginal = d.GrossPerShare is { } perShare && d.Quantity > 0 ? perShare * d.Quantity : null;
        var grossEur = grossOriginal is { } g ? ToEuro(g, d.InstrumentCurrency, d.PaidOn, rates, missingRates) : null;

        // The tax withheld, most exact first: the API's gross minus the report's net per share (same
        // currency, no rounding); else the report's withholding (rounded to cents); else estimated below.
        decimal? withheldOriginal = null;
        string? withheldCurrency = null;
        if (d.GrossFromApi && d.GrossPerShare is { } gps && d.NetPerShare is { } nps && d.Quantity > 0)
            (withheldOriginal, withheldCurrency) = (Math.Max(0, (gps - nps) * d.Quantity), d.InstrumentCurrency);
        else if (d.WithholdingTax is { } tax)
            (withheldOriginal, withheldCurrency) = (Math.Abs(tax), d.WithholdingTaxCurrency ?? d.InstrumentCurrency);

        var withheldEur = withheldOriginal is { } wo ? ToEuro(wo, withheldCurrency, d.PaidOn, rates, missingRates) : null;

        if (!d.GrossFromApi && grossOriginal is not null)
            flags |= IrsFlags.GrossDerived;

        if (withheldOriginal is null && grossEur is { } ge && netEur is { } ne)
        {
            withheldEur = Math.Max(0, ge - ne);
            flags |= IrsFlags.WithholdingEstimated;
        }

        if (grossOriginal is null && netEur is { } net && withheldEur is { } withheld)
        {
            grossEur = net + withheld;
            flags |= IrsFlags.GrossDerived;
        }

        if ((grossOriginal is not null && grossEur is null) || netEur is null || (withheldOriginal is not null && withheldEur is null))
            flags |= IrsFlags.MissingRate;

        if (grossEur is { } gross && withheldEur is { } w && netEur is { } n && (flags & (IrsFlags.GrossDerived | IrsFlags.WithholdingEstimated)) == 0)
        {
            var difference = Math.Abs(gross - w - n);
            if (difference > Math.Max(MismatchFloor, gross * MismatchShare))
                flags |= IrsFlags.Mismatch;
        }

        // In the original currency when possible: cents of rounding would distort small dividends.
        decimal? effective = withheldOriginal is { } wOrig && grossOriginal is > 0 && withheldCurrency == d.InstrumentCurrency
            ? Math.Round(wOrig / grossOriginal.Value, 4)
            : grossEur is > 0 && withheldEur is { } wr ? Math.Round(wr / grossEur.Value, 4) : null;
        // Portugal's own withholding (28 %, or 35 % on omnibus accounts) is not a treaty matter.
        var treaty = country == DomesticCountry ? null : TreatyRates.For(country, d.PaidOn.UtcDateTime.Year);
        if (effective > (treaty?.CreditRate ?? UsualTreatyRate) + WarningMargin && country != DomesticCountry)
            flags |= IrsFlags.HighWithholding;

        decimal? creditCap = treaty is not null && grossEur is { } capBase ? Math.Round(capBase * treaty.CreditRate, 2) : null;

        var rate = d.InstrumentCurrency is { } currency ? rates.RateFor(RateTable.Normalise(0, currency).Currency, date) : null;
        return new IrsDividend(d, country, grossOriginal, rate?.Date, rate?.PerEuro, grossEur, withheldEur, netEur, effective, flags, treaty, creditCap);
    }

    private static decimal? ToEuro(decimal amount, string? currency, DateTimeOffset when, RateTable rates, ISet<string> missingRates)
    {
        if (string.IsNullOrEmpty(currency))
            return null;

        var date = DateOnly.FromDateTime(when.UtcDateTime);
        var result = rates.ToEuro(amount, currency, date);
        if (result is null)
            missingRates.Add($"{RateTable.Normalise(0, currency).Currency} {date:yyyy-MM-dd}");
        return result;
    }

    /// <summary>The tax year a refund note refers to: "2024 US dividends withholding tax adjustment" → 2024.</summary>
    public static int? YearOf(string? note) =>
        note is not null && YearInNote().Match(note) is { Success: true } match ? int.Parse(match.Value) : null;

    /// <summary>The country a refund note names; Trading 212's notes so far are about US withholding.</summary>
    public static string? CountryOfNote(string? note) =>
        note is not null && Regex.IsMatch(note, @"\bUS\b") ? "US" : null;

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearInNote();
}
