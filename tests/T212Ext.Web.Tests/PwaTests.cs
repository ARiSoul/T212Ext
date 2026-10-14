using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Arisoul.T212Ext.Web.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Web.Tests;

public class PwaTests
{
    private static readonly string WebRoot = Path.Combine(RepositoryRoot(), "src", "T212Ext.Web", "wwwroot");

    private static JsonElement Manifest(string culture)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
            var json = JsonSerializer.Serialize(Pwa.Manifest(new StringLocalizer<SharedResource>(factory)));
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void The_manifest_has_what_browsers_need_to_install_the_app()
    {
        var manifest = Manifest("en-GB");

        Assert.Equal("T212Ext", manifest.GetProperty("name").GetString());
        Assert.Equal("/", manifest.GetProperty("start_url").GetString());
        Assert.Equal("standalone", manifest.GetProperty("display").GetString());

        var icons = manifest.GetProperty("icons").EnumerateArray().ToList();
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "192x192");
        Assert.Contains(icons, i => i.GetProperty("sizes").GetString() == "512x512" && !i.TryGetProperty("purpose", out _));
        Assert.Contains(icons, i => i.TryGetProperty("purpose", out var p) && p.GetString() == "maskable");
    }

    [Fact]
    public void Every_icon_the_manifest_and_the_page_name_exists()
    {
        var manifest = Manifest("en-GB");
        var files = manifest.GetProperty("icons").EnumerateArray()
            .Concat(manifest.GetProperty("shortcuts").EnumerateArray().SelectMany(s => s.GetProperty("icons").EnumerateArray()))
            .Select(i => i.GetProperty("src").GetString()!)
            .Append("icons/apple-touch-icon.png")   // App.razor
            .Append("service-worker.js")            // js/app.js
            .Distinct();

        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(WebRoot, f)), $"{f} is missing from wwwroot"));
    }

    [Fact]
    public void The_texts_follow_the_language()
    {
        var portuguese = Manifest("pt-PT");

        Assert.Equal("pt-PT", portuguese.GetProperty("lang").GetString());
        Assert.Equal(["Dividendos", "Relatório de IRS"], portuguese.GetProperty("shortcuts").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        Assert.NotEqual(Manifest("en-GB").GetProperty("description").GetString(), portuguese.GetProperty("description").GetString());
    }

    private static string RepositoryRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}
