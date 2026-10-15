using System.Globalization;
using System.Reflection;

namespace Arisoul.T212Ext.Web.Components.Shared;

/// <summary>How numbers, dates and the version are shown, in the current culture.</summary>
public static class Fmt
{
    public static string Version { get; } =
        typeof(Fmt).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    public static string Money(decimal? value, string? currency) =>
        value is null ? "—" : $"{value.Value:N2} {currency}".TrimEnd();

    public static string Number(decimal? value) =>
        value is null ? "—" : value.Value.ToString("#,##0.##########", CultureInfo.CurrentCulture);

    /// <summary>A fraction as a percentage: 0.0345 is "3,45 %" in Portuguese.</summary>
    public static string Percent(decimal? fraction, int decimals = 2) =>
        fraction is null ? "—" : fraction.Value.ToString("P" + decimals, CultureInfo.CurrentCulture);

    public static string Date(DateTimeOffset? value) =>
        value is null ? "—" : value.Value.UtcDateTime.ToString("d", CultureInfo.CurrentCulture);

    public static string Date(DateOnly? value) =>
        value is null ? "—" : value.Value.ToString("d", CultureInfo.CurrentCulture);

    public static string DateTime(DateTimeOffset? value) =>
        value is null ? "—" : value.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public static string Month(int year, int month) =>
        new DateOnly(year, month, 1).ToString("MMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>A short month label for chart axes: "nov. 25".</summary>
    public static string MonthShort(int year, int month) =>
        new DateOnly(year, month, 1).ToString("MMM yy", CultureInfo.CurrentCulture);

    /// <summary>A weekday in three letters: "seg", "Mon", "lun" (some cultures' abbreviations are the full name).</summary>
    public static string DayShort(DayOfWeek day)
    {
        var name = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day).TrimEnd('.');
        return name.Length > 3 ? name[..3] : name;
    }

    public static string MonthName(int month) =>
        CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month);

    public static string SignClass(decimal? value) =>
        value > 0 ? "text-positive" : value < 0 ? "text-negative" : "";
}
