using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Arisoul.T212Ext.Web.Components.Shared;

namespace Arisoul.T212Ext.Web.Tests;

public partial class LocalizationTests
{
    private static readonly string Resources = Path.Combine(RepositoryRoot(), "src", "T212Ext.Web", "Resources");
    private static readonly string Components = Path.Combine(RepositoryRoot(), "src", "T212Ext.Web", "Components");

    private static Dictionary<string, string> Load(string suffix) =>
        XDocument.Load(Path.Combine(Resources, $"SharedResource{suffix}.resx"))
            .Root!.Elements("data")
            .ToDictionary(d => d.Attribute("name")!.Value, d => d.Element("value")!.Value);

    [Theory]
    [InlineData(".pt")]
    [InlineData(".es")]
    public void Every_language_has_every_key_and_the_same_placeholders(string suffix)
    {
        var english = Load("");
        var other = Load(suffix);

        Assert.Equal(english.Keys.Order(), other.Keys.Order());
        foreach (var (key, text) in english)
        {
            Assert.False(string.IsNullOrWhiteSpace(other[key]), $"{key} is empty in {suffix}");
            Assert.Equal(Placeholders(text), Placeholders(other[key]));
        }
    }

    [Fact]
    public void Every_key_the_components_use_exists()
    {
        var keys = Load("").Keys.ToHashSet();
        var used = Directory.EnumerateFiles(Components, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor") || f.EndsWith(".cs"))
            .SelectMany(f => KeyUse().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Distinct();

        var missing = used.Where(k => !keys.Contains(k)).ToList();
        Assert.True(missing.Count == 0, "Missing keys: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData(null, "pt-PT")]
    [InlineData("en-GB", "en-GB")]
    [InlineData("es", "es-ES")]
    [InlineData("EN-us", "en-GB")]
    [InlineData("fr-FR", "pt-PT")]
    public void Unknown_or_partial_cultures_resolve_to_a_supported_one(string? culture, string expected) =>
        Assert.Equal(expected, Languages.Resolve(culture));

    [Theory]
    [InlineData("DIVIDENDS_PAID_BY_US_CORPORATIONS", "Dividends paid by us corporations")]
    [InlineData("ORDINARY", "Ordinary")]
    [InlineData("Ordinary", "Ordinary")]
    [InlineData(null, "—")]
    public void Codes_without_a_translation_read_as_words(string? code, string expected) =>
        Assert.Equal(expected, Labels.Humanize(code));

    private static string[] Placeholders(string text) => [.. Placeholder().Matches(text).Select(m => m.Value).Order()];

    // From this source file, not the binary: builds can put the binaries anywhere (--artifacts-path).
    private static string RepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "T212Ext.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"L\[""([A-Za-z0-9_]+)""\]")]
    private static partial Regex KeyUse();
}
