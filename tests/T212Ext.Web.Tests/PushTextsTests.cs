using System.Globalization;
using Arisoul.T212Ext.Core.Notifications;
using Arisoul.T212Ext.Web.Auth;
using Arisoul.T212Ext.Web.Resources;
using Arisoul.T212Ext.Web.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Web.Tests;

public class PushTextsTests
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-PT");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    private static readonly PushTexts Texts = new(new StringLocalizer<SharedResource>(
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance)));

    private static DividendNotice Dividend(string name, decimal amount) => new(name, amount, "EUR", DateTimeOffset.UnixEpoch);

    [Fact]
    public void One_dividend_names_it_with_its_amount_in_the_devices_language()
    {
        var text = Texts.NewDividends(Portuguese, [Dividend("Apple", 0.82m)]);

        Assert.Equal("Dividendo recebido", text.Title);
        Assert.Equal("Apple: 0,82 EUR", text.Body);
    }

    [Fact]
    public void Several_dividends_name_the_first_ones_and_count_the_rest()
    {
        var text = Texts.NewDividends(English, [Dividend("Apple", 0.82m), Dividend("Microsoft", 0.65m), Dividend("Realty Income", 1.2m), Dividend("Vanguard", 3m)]);

        Assert.Equal("4 dividends received", text.Title);
        Assert.Equal("Apple 0.82 EUR, Microsoft 0.65 EUR, Realty Income 1.20 EUR and 1 more", text.Body);
    }

    [Fact]
    public void The_monthly_summary_compares_with_the_same_month_a_year_before()
    {
        var month = new MonthNotice(new DateOnly(2026, 9, 1), [("EUR", 12.3m, 10m), ("USD", 4m, null)], 7);

        var text = Texts.MonthlySummary(Portuguese, month);

        Assert.Equal("Dividendos de setembro 2026", text.Title);
        Assert.StartsWith("12,30 EUR (+23", text.Body);
        Assert.Contains("face ao ano anterior), 4,00 USD em 7 pagamentos.", text.Body);
    }

    [Fact]
    public void Texts_do_not_change_the_callers_culture()
    {
        var before = CultureInfo.CurrentUICulture;

        Texts.Test(Portuguese);

        Assert.Equal(before, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void New_vapid_keys_prints_both_settings()
    {
        var output = new StringWriter();
        var exit = AuthCommands.Run(["new-vapid-keys"], new StringReader(""), output, new StringWriter());

        var lines = output.ToString().Trim().Split(Environment.NewLine);
        Assert.Equal(0, exit);
        Assert.StartsWith("Push__PublicKey=", lines[0]);
        Assert.StartsWith("Push__PrivateKey=", lines[1]);
        Assert.True(AuthCommands.IsCommand(["new-vapid-keys"]));
    }
}
