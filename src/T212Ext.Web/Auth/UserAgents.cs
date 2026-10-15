namespace Arisoul.T212Ext.Web.Auth;

/// <summary>A browser's User-Agent in a few words, "Android · Chrome", for the sign-in history.</summary>
public static class UserAgents
{
    // Order matters: Edge and Opera say "Chrome" too, Chrome says "Safari", and iPhones say "Mac OS X".
    private static readonly (string Token, string Name)[] Systems =
    [
        ("iPhone", "iPhone"), ("iPad", "iPad"), ("Android", "Android"), ("Windows", "Windows"),
        ("Mac OS X", "macOS"), ("CrOS", "ChromeOS"), ("Linux", "Linux"),
    ];

    private static readonly (string Token, string Name)[] Browsers =
    [
        ("Edg", "Edge"), ("OPR", "Opera"), ("SamsungBrowser", "Samsung Internet"), ("Firefox", "Firefox"), ("FxiOS", "Firefox"),
        ("CriOS", "Chrome"), ("Chrome", "Chrome"), ("Safari", "Safari"), ("curl", "curl"),
    ];

    /// <summary>Null when there is no User-Agent; the raw text, shortened, when nothing in it is known.</summary>
    public static string? Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return null;

        var system = Systems.FirstOrDefault(s => userAgent.Contains(s.Token, StringComparison.Ordinal)).Name;
        var browser = Browsers.FirstOrDefault(b => userAgent.Contains(b.Token + "/", StringComparison.Ordinal)).Name;
        return (system, browser) switch
        {
            (null, null) => userAgent.Length <= 40 ? userAgent : userAgent[..40] + "…",
            (null, _) => browser,
            (_, null) => system,
            _ => $"{system} · {browser}",
        };
    }
}
