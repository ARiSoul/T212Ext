using Microsoft.AspNetCore.Http;

namespace Arisoul.T212Ext.Web.Tests;

public class SecurityHeadersTests
{
    private static DefaultHttpContext Request()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("t212.example.org");
        SecurityHeaders.Apply(context);
        return context;
    }

    private static Dictionary<string, string> Directives(string policy) =>
        policy.Split("; ").Select(d => d.Split(' ', 2)).ToDictionary(d => d[0], d => d.Length > 1 ? d[1] : "");

    [Fact]
    public void Inline_scripts_need_this_requests_nonce()
    {
        var context = Request();
        var nonce = SecurityHeaders.Nonce(context);
        var script = Directives(context.Response.Headers.ContentSecurityPolicy!)["script-src"];

        Assert.NotEmpty(nonce);
        Assert.Contains($"'nonce-{nonce}'", script);
        Assert.DoesNotContain("'unsafe-inline'", script);
        Assert.NotEqual(nonce, SecurityHeaders.Nonce(Request()));   // new on every request
    }

    [Fact]
    public void Nothing_is_loaded_from_or_framed_by_other_sites()
    {
        var headers = Request().Response.Headers;
        var policy = Directives(headers.ContentSecurityPolicy!);

        Assert.Equal("'self'", policy["default-src"]);
        Assert.Equal("'none'", policy["frame-ancestors"]);
        Assert.Equal("'none'", policy["object-src"]);
        Assert.Equal("'self' data: blob:", policy["img-src"]);   // ApexCharts' images made in the browser
        Assert.Equal("'self' wss://t212.example.org ws://t212.example.org", policy["connect-src"]);
        Assert.Equal("DENY", headers.XFrameOptions);
        Assert.Equal("nosniff", headers.XContentTypeOptions);
        Assert.Equal("same-origin", headers["Referrer-Policy"]);
    }
}
