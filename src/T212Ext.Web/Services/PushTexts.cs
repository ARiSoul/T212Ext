using System.Globalization;
using Arisoul.T212Ext.Core.Notifications;
using Arisoul.T212Ext.Web.Components.Shared;
using Arisoul.T212Ext.Web.Resources;
using Microsoft.Extensions.Localization;

namespace Arisoul.T212Ext.Web.Services;

/// <summary>The notifications' texts, from the app's translations, in each device's language.</summary>
public sealed class PushTexts(IStringLocalizer<SharedResource> l) : IPushTexts
{
    /// <summary>How many dividends a notification names; the rest are counted.</summary>
    public const int Named = 3;

    public PushText NewDividends(CultureInfo culture, IReadOnlyList<DividendNotice> dividends) => In(culture, () =>
    {
        if (dividends.Count == 1)
        {
            var only = dividends[0];
            return new PushText(l["Push_Dividend_Title"], $"{only.Name}: {Fmt.Money(only.Amount, only.Currency)}");
        }

        var named = string.Join(", ", dividends.Take(Named).Select(d => $"{d.Name} {Fmt.Money(d.Amount, d.Currency)}"));
        var body = dividends.Count > Named ? $"{named} {string.Format(culture, l["Push_AndMore"], dividends.Count - Named)}" : named;
        return new PushText(string.Format(culture, l["Push_Dividends_Title"], dividends.Count), body);
    });

    public PushText MonthlySummary(CultureInfo culture, MonthNotice month) => In(culture, () =>
    {
        var name = month.Month.ToString("MMMM yyyy", culture);
        var parts = month.Totals.Select(t =>
        {
            var amount = Fmt.Money(t.Amount, t.Currency);
            if (t.YearBefore is not > 0)
                return amount;
            var change = (t.Amount - t.YearBefore.Value) / t.YearBefore.Value;
            return $"{amount} ({string.Format(culture, l["Push_Summary_Change"], (change > 0 ? "+" : "") + Fmt.Percent(change, 0))})";
        });
        return new PushText(
            string.Format(culture, l["Push_Summary_Title"], name),
            string.Format(culture, l["Push_Summary_Body"], string.Join(", ", parts), month.Count));
    });

    public PushText Test(CultureInfo culture) => In(culture, () => new PushText(l["Push_Test_Title"], l["Push_Test_Body"]));

    // The translations and Fmt read the current culture: set it for the time of one text.
    private static PushText In(CultureInfo culture, Func<PushText> write)
    {
        var (previous, previousUi) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        try
        {
            return write();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (previous, previousUi);
        }
    }
}
