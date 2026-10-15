using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Arisoul.T212Ext.Web.Components.Shared;
using Arisoul.T212Ext.Web.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Web.Tests;

public class LabelsTests
{
    // The base types of the API's HistoryDividendItem.type (each also comes with "_MANUFACTURED_PAYMENT").
    private static readonly string[] ApiTypes =
    [
        "ORDINARY", "BONUS", "PROPERTY_INCOME", "RETURN_OF_CAPITAL_NON_US", "DEMERGER", "INTEREST", "CAPITAL_GAINS_DISTRIBUTION_NON_US",
        "INTERIM_LIQUIDATION", "INTEREST_PAID_BY_US_OBLIGORS", "INTEREST_PAID_BY_FOREIGN_CORPORATIONS", "DIVIDENDS_PAID_BY_US_CORPORATIONS",
        "DIVIDENDS_PAID_BY_FOREIGN_CORPORATIONS", "CAPITAL_GAINS", "REAL_PROPERTY_INCOME_AND_NATURAL_RESOURCES_ROYALTIES", "OTHER_INCOME",
        "QUALIFIED_INVESTMENT_ENTITY", "TRUST_DISTRIBUTION", "PUBLICLY_TRADED_PARTNERSHIP_DISTRIBUTION", "CAPITAL_GAINS_DISTRIBUTION",
        "RETURN_OF_CAPITAL", "OTHER_DIVIDEND_EQUIVALENT", "TAX_EVENT_1446F_FOR_PUBLICLY_TRADED_SECURITIES", "PTP_UNCHARACTERISED_INCOME",
        "MULTIPLE_1042S_TAX_COMPONENTS", "DIVIDEND", "SHORT_TERM_CAPITAL_GAINS", "LONG_TERM_CAPITAL_GAINS", "PROPERTY_INCOME_DISTRIBUTION",
        "TAX_EXEMPTED",
    ];

    private static readonly IStringLocalizer<SharedResource> L = new StringLocalizer<SharedResource>(
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));

    private static string In(string culture, Func<string> text)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return text();
        }
        finally
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = previous;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(".pt")]
    [InlineData(".es")]
    public void Every_api_dividend_type_is_translated(string suffix)
    {
        var keys = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "T212Ext.Web", "Resources", $"SharedResource{suffix}.resx"))
            .Root!.Elements("data").Select(d => d.Attribute("name")!.Value).ToHashSet();

        var missing = ApiTypes.Where(t => !keys.Contains($"DividendType_{t}")).ToList();

        Assert.True(missing.Count == 0, "Missing: " + string.Join(", ", missing));
        Assert.Contains("DividendType_Manufactured", keys);
    }

    [Theory]
    [InlineData("DIVIDENDS_PAID_BY_US_CORPORATIONS", "Dividends paid by US corporations")]
    [InlineData("Dividends paid by us corporations", "Dividends paid by US corporations")]   // the CSV's words
    [InlineData("Ordinary", "Ordinary")]
    [InlineData("DIVIDEND_MANUFACTURED_PAYMENT", "Dividend (manufactured payment, lent shares)")]
    [InlineData("Dividend manufactured payment", "Dividend (manufactured payment, lent shares)")]
    [InlineData("SOMETHING_NEW", "Something new")]
    [InlineData("SOMETHING_NEW_MANUFACTURED_PAYMENT", "Something new (manufactured payment, lent shares)")]
    [InlineData(null, "—")]
    public void Api_codes_and_csv_words_get_the_same_name(string? type, string expected) =>
        Assert.Equal(expected, In("en-GB", () => Labels.DividendType(L, type)));

    [Fact]
    public void Types_follow_the_language()
    {
        Assert.Equal("Dividendos pagos por empresas dos EUA", In("pt-PT", () => Labels.DividendType(L, "DIVIDENDS_PAID_BY_US_CORPORATIONS")));
        Assert.Equal("Isento de imposto (compensação, ações emprestadas)", In("pt-PT", () => Labels.DividendType(L, "TAX_EXEMPTED_MANUFACTURED_PAYMENT")));
    }

    [Theory]
    [InlineData("AAPL_US_EQ", "https://www.trading212.com/trading-instruments/invest/AAPL.US")]
    [InlineData("BRK.B_US_EQ", "https://www.trading212.com/trading-instruments/invest/BRK.B.US")]
    [InlineData("VUSAl_EQ", "https://www.trading212.com/trading-instruments/invest/VUSA.GB")]
    [InlineData("VWCEd_EQ", null)]   // Xetra: no confirmed address
    [InlineData("AAPL", null)]       // a CSV symbol
    [InlineData("<x>_US_EQ", null)]
    [InlineData(null, null)]
    public void Only_known_ticker_forms_link_to_trading_212(string? ticker, string? page) =>
        Assert.Equal(page, Labels.TradingPage(ticker));

    private static string RepositoryRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
