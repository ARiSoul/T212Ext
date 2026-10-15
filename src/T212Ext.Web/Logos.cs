using Arisoul.T212Ext.Core.Logos;

namespace Arisoul.T212Ext.Web;

/// <summary>
/// Instrument icons: <c>/logos/{ticker}?isin=…</c> answers the stored logo, or an avatar with the
/// ticker's code when there is none. The browser only ever asks this app.
/// </summary>
public static class Logos
{
    // Text on all of them has at least 5.3:1 contrast with white.
    private static readonly string[] AvatarColours = ["#1f5fae", "#b4461b", "#127a57", "#8a5a00", "#a8355f", "#0a6b0a", "#5b4cb8", "#3f5872"];

    /// <summary>The code an avatar shows: the ticker's symbol ("AAPL_US_EQ" → "AAPL"), letters and digits, at most 4.</summary>
    public static string Code(string ticker)
    {
        var symbol = new string(ticker.Split('_')[0].Where(char.IsAsciiLetterOrDigit).Take(4).ToArray()).ToUpperInvariant();
        return symbol.Length > 0 ? symbol : "?";
    }

    /// <summary>The same colour for the same code, everywhere and on every run (FNV-1a, not string.GetHashCode).</summary>
    public static string Colour(string code)
    {
        var hash = 2166136261u;
        foreach (var c in code)
            hash = (hash ^ c) * 16777619u;
        return AvatarColours[hash % (uint)AvatarColours.Length];
    }

    public static string AvatarSvg(string ticker)
    {
        var code = Code(ticker);
        var size = code.Length switch { <= 2 => 28, 3 => 22, _ => 18 };
        return $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect width="64" height="64" rx="14" fill="{Colour(code)}"/><text x="32" y="32" dy=".35em" text-anchor="middle" font-family="system-ui,-apple-system,'Segoe UI',Roboto,sans-serif" font-weight="700" font-size="{size}" fill="#ffffff">{code}</text></svg>""";
    }

    public static WebApplication MapLogos(this WebApplication app)
    {
        app.MapGet("/logos/{ticker}", async (string ticker, string? isin, InstrumentLogos logos, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (ticker.Length > 100 || isin?.Length > 20)
                return Results.BadRequest();

            var logo = await logos.GetAsync(ticker, isin, cancellationToken);
            context.Response.Headers.CacheControl = "private, max-age=86400";
            // A logo may be a third party's SVG: opened on its own, it can run nothing and load nothing.
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
            return logo is null
                ? Results.Text(AvatarSvg(ticker), "image/svg+xml")
                : Results.File(logo.Data, logo.ContentType);
        });
        return app;
    }
}
