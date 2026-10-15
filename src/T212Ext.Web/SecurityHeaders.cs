using System.Security.Cryptography;

namespace Arisoul.T212Ext.Web;

/// <summary>
/// The headers every response carries: no framing, no MIME sniffing, no full URLs sent to other
/// sites, and a Content-Security-Policy. Scripts run only from this site, or inline with the
/// request's nonce (the theme script and the import map in <c>App.razor</c>).
/// </summary>
/// <remarks>
/// <para><c>'unsafe-eval'</c>: Blazor-ApexCharts turns the chart formatters (<c>ChartStyle.Money</c>
/// and the like, sent as text) into functions with <c>eval</c>. Injected markup still cannot run a
/// script: inline scripts need the nonce, and other sites' scripts are refused.</para>
/// <para><c>style-src 'unsafe-inline'</c>: Blazor writes <c>style</c> attributes and ApexCharts adds
/// style elements. Styles cannot run code.</para>
/// <para>An endpoint may set its own, stricter policy (the logos' sandbox): it replaces this one.</para>
/// </remarks>
public static class SecurityHeaders
{
    private const string NonceKey = "t212ext:csp-nonce";

    /// <summary>The nonce of this request's policy, for the inline scripts of the page.</summary>
    public static string Nonce(HttpContext context) => (string?)context.Items[NonceKey] ?? "";

    public static string Policy(string nonce, string host) => string.Join("; ",
        "default-src 'self'",
        $"script-src 'self' 'nonce-{nonce}' 'unsafe-eval'",
        "style-src 'self' 'unsafe-inline'",
        "img-src 'self' data:",
        "font-src 'self'",
        // Blazor's circuit is a WebSocket to this host; not every browser counts ws(s) as 'self'.
        $"connect-src 'self' wss://{host} ws://{host}",
        "manifest-src 'self'",
        "worker-src 'self'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
        "frame-ancestors 'none'");

    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use((context, next) =>
        {
            Apply(context);
            return next();
        });
        return app;
    }

    /// <summary>Sets the headers, with a new nonce for this request.</summary>
    public static void Apply(HttpContext context)
    {
        // Hex rather than Base64: the same in the header and in the HTML, where Razor would encode '+'.
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceKey] = nonce;

        var headers = context.Response.Headers;
        headers.XFrameOptions = "DENY";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "same-origin";
        headers.ContentSecurityPolicy = Policy(nonce, context.Request.Host.Value ?? "");
    }
}
