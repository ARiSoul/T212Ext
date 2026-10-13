namespace Arisoul.T212Ext.Web;

/// <summary>The languages of the app. Each has a resource file (Resources/SharedResource.*.resx).</summary>
public static class Languages
{
    public sealed record Language(string Culture, string NativeName, string Code);

    public static IReadOnlyList<Language> All { get; } =
    [
        new("pt-PT", "Português", "PT"),
        new("en-GB", "English", "EN"),
        new("es-ES", "Español", "ES"),
    ];

    /// <summary>A supported culture for <paramref name="culture"/> (by exact name or language), else Portuguese.</summary>
    public static string Resolve(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return All[0].Culture;

        return All.FirstOrDefault(l => l.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase))?.Culture
            ?? All.FirstOrDefault(l => l.Culture.StartsWith(culture.Split('-')[0] + "-", StringComparison.OrdinalIgnoreCase))?.Culture
            ?? All[0].Culture;
    }
}
