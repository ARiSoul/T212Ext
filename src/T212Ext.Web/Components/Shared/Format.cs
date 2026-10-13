using System.Reflection;

namespace Arisoul.T212Ext.Web.Components.Shared;

/// <summary>How numbers, dates and the version are shown. Culture comes from <c>Ui:Culture</c>.</summary>
public static class Format
{
    public static string Version { get; } =
        typeof(Format).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    public static string Money(decimal? value, string? currency) =>
        value is null ? "—" : $"{value.Value:N2} {currency}".TrimEnd();

    public static string Number(decimal? value) =>
        value is null ? "—" : value.Value.ToString("0.##########");

    public static string Date(DateTimeOffset? value) =>
        value is null ? "—" : value.Value.UtcDateTime.ToString("d");

    public static string DateTime(DateTimeOffset? value) =>
        value is null ? "—" : value.Value.ToLocalTime().ToString("g");

    public static string SignClass(decimal? value) =>
        value > 0 ? "text-success" : value < 0 ? "text-danger" : "";
}
