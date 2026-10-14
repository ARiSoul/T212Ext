using System.Globalization;
using System.Text.Json;
using Arisoul.T212Ext.Web.Resources;
using Microsoft.Extensions.Localization;

namespace Arisoul.T212Ext.Web;

/// <summary>
/// The web app manifest, which makes the site installable on a phone or a computer. It is built per
/// request so its texts (description, shortcut names) follow the language chosen in the app: the
/// page asks for it with credentials, so the language cookie comes along.
/// </summary>
public static class Pwa
{
    public const string ManifestPath = "/manifest.webmanifest";

    // The dark theme's background (--app-bg in app.css): the splash screen and the title bar.
    public const string BackgroundColour = "#111214";

    public static object Manifest(IStringLocalizer<SharedResource> l) => new
    {
        name = "T212Ext",
        short_name = "T212Ext",
        description = l["Pwa_Description"].Value,
        lang = CultureInfo.CurrentUICulture.Name,
        id = "/",
        start_url = "/",
        scope = "/",
        display = "standalone",
        background_color = BackgroundColour,
        theme_color = BackgroundColour,
        categories = new[] { "finance" },
        icons = new object[]
        {
            new { src = "icons/icon-192.png", sizes = "192x192", type = "image/png" },
            new { src = "icons/icon-512.png", sizes = "512x512", type = "image/png" },
            new { src = "icons/icon-maskable-512.png", sizes = "512x512", type = "image/png", purpose = "maskable" },
            new { src = "favicon.svg", sizes = "any", type = "image/svg+xml" },
        },
        shortcuts = new object[]
        {
            Shortcut(l["Nav_Dividends"], "/dividends"),
            Shortcut(l["Nav_Tax"], "/tax"),
        },
    };

    public static WebApplication MapPwa(this WebApplication app)
    {
        app.MapGet(ManifestPath, (IStringLocalizer<SharedResource> l) =>
                Results.Text(JsonSerializer.Serialize(Manifest(l)), "application/manifest+json"))
            .AllowAnonymous();
        return app;
    }

    private static object Shortcut(string name, string url) => new
    {
        name,
        url,
        icons = new[] { new { src = "icons/icon-192.png", sizes = "192x192", type = "image/png" } },
    };
}
