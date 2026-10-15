using Arisoul.T212Ext.Core.Tax;

namespace Arisoul.T212Ext.Core.Tests;

public class AggregationTests
{
    [Theory]
    [InlineData(2025, 83_000, 696, AggregationVerdict.Mandatory)]      // exactly the top bracket
    [InlineData(2025, 83_000, 695.99, AggregationVerdict.NotMandatory)]
    [InlineData(2026, 83_000, 696, AggregationVerdict.NotMandatory)]   // 2026's bracket is higher
    [InlineData(2026, 86_000, 1_000, AggregationVerdict.Mandatory)]
    public void The_balance_must_be_aggregated_when_income_with_it_reaches_the_top_bracket(int year, double income, double balance, AggregationVerdict expected)
    {
        var check = Aggregation.Check(year, (decimal)balance, (decimal)income);

        Assert.Equal(expected, check.Verdict);
        Assert.Equal((decimal)income + (decimal)balance, check.IncomeWithBalance);
        Assert.Equal(Aggregation.TopBracket[year], check.TopBracket);
    }

    [Fact]
    public void Without_a_positive_short_term_balance_there_is_nothing_to_aggregate()
    {
        Assert.Equal(AggregationVerdict.NoShortTermGain, Aggregation.Check(2025, 0, 100_000).Verdict);
        Assert.Equal(AggregationVerdict.NoShortTermGain, Aggregation.Check(2025, -50, 100_000).Verdict);
    }

    [Fact]
    public void The_income_is_needed_and_unknown_years_are_said_so()
    {
        Assert.Equal(AggregationVerdict.IncomeNeeded, Aggregation.Check(2025, 10, null).Verdict);
        Assert.Equal(AggregationVerdict.UnknownYear, Aggregation.Check(2030, 10, 50_000).Verdict);
    }
}
