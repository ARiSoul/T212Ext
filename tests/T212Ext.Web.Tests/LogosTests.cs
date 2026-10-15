namespace Arisoul.T212Ext.Web.Tests;

public class LogosTests
{
    [Theory]
    [InlineData("AAPL_US_EQ", "AAPL")]
    [InlineData("VWCEd_EQ", "VWCE")]
    [InlineData("BRK.B_US_EQ", "BRKB")]
    [InlineData("GOOGL_US_EQ", "GOOG")]
    [InlineData("_EQ", "?")]
    public void The_avatar_shows_the_tickers_symbol(string ticker, string code) =>
        Assert.Equal(code, Logos.Code(ticker));

    [Fact]
    public void The_same_symbol_always_gets_the_same_colour()
    {
        Assert.Equal(Logos.Colour("AAPL"), Logos.Colour("AAPL"));
        Assert.Matches("^#[0-9a-f]{6}$", Logos.Colour("MSFT"));
        Assert.True(new[] { "AAPL", "MSFT", "O", "VWCE", "ASML", "KO", "PEP", "JNJ" }.Select(Logos.Colour).Distinct().Count() > 2);
    }

    [Fact]
    public void The_avatar_is_an_svg_with_only_the_code_in_it()
    {
        var svg = Logos.AvatarSvg("<script>_EQ");

        Assert.StartsWith("<svg", svg);
        Assert.DoesNotContain("<script", svg);
        Assert.Contains(">SCRI<", svg);
    }
}
