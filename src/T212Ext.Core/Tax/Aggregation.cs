namespace Arisoul.T212Ext.Core.Tax;

public enum AggregationVerdict
{
    /// <summary>No positive balance from holdings under 365 days: nothing to aggregate.</summary>
    NoShortTermGain,

    /// <summary>The year's other taxable income is needed to tell.</summary>
    IncomeNeeded,

    /// <summary>The top bracket of that year is not in the table yet.</summary>
    UnknownYear,

    /// <summary>Taxable income, with the balance, reaches the top bracket: aggregation is mandatory.</summary>
    Mandatory,

    /// <summary>It stays below the top bracket: the 28% rate applies unless aggregation is chosen.</summary>
    NotMandatory,
}

public sealed record AggregationCheck(AggregationVerdict Verdict, decimal? TopBracket, decimal? IncomeWithBalance);

/// <summary>
/// Whether the positive balance of holdings under 365 days must be aggregated (englobamento
/// obrigatório, Código do IRS art. 72.º n.º 14): when the taxable income, including that balance,
/// reaches the top bracket of art. 68.º. See docs/TAX.md.
/// </summary>
public static class Aggregation
{
    /// <summary>The start of the top bracket of art. 68.º, by income year.</summary>
    public static readonly IReadOnlyDictionary<int, decimal> TopBracket = new Dictionary<int, decimal>
    {
        [2025] = 83_696m,
        [2026] = 86_634m,
    };

    /// <param name="otherTaxableIncome">The year's taxable income (rendimento coletável) without these gains; null when not given.</param>
    public static AggregationCheck Check(int year, decimal shortTermBalance, decimal? otherTaxableIncome)
    {
        var bracket = TopBracket.TryGetValue(year, out var value) ? value : (decimal?)null;

        if (shortTermBalance <= 0)
            return new(AggregationVerdict.NoShortTermGain, bracket, null);
        if (bracket is null)
            return new(AggregationVerdict.UnknownYear, null, null);
        if (otherTaxableIncome is not { } income)
            return new(AggregationVerdict.IncomeNeeded, bracket, null);

        var total = income + shortTermBalance;
        return new(total >= bracket ? AggregationVerdict.Mandatory : AggregationVerdict.NotMandatory, bracket, total);
    }
}
