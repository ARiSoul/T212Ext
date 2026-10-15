using System.Globalization;
using Arisoul.T212Ext.Web.Components.Grid;
using Arisoul.T212Ext.Web.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Arisoul.T212Ext.Web.Tests;

/// <summary>The grid component in a test renderer (bUnit): what the person clicks, and what it shows.</summary>
public sealed class DataGridTests : IDisposable
{
    private sealed record Row(string Name, string Kind, DateTimeOffset Date);

    private static readonly IReadOnlyList<Row> Rows =
    [
        new("Apple", "Stock", new(2025, 3, 1, 0, 0, 0, TimeSpan.Zero)),
        new("Vanguard", "ETF", new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        new("Realty Income", "Stock", new(2024, 7, 1, 0, 0, 0, TimeSpan.Zero)),
    ];

    private static readonly IReadOnlyList<GridColumn<Row>> Columns =
    [
        new() { Key = "name", Title = "Name", Value = r => r.Name, MobileTitle = true },
        new() { Key = "kind", Title = "Kind", Value = r => r.Kind },
        new() { Key = "date", Title = "Date", Value = r => r.Date, RangeFilter = true },
    ];

    private readonly BunitContext _context = new();
    private readonly CultureInfo _culture = CultureInfo.CurrentUICulture;

    public DataGridTests()
    {
        // English texts, whatever the machine's language.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-GB");
        _context.Services.AddLocalization();
        _context.Services.AddScoped<BrowserStorage>();
        // Browser storage answers nothing and keeps nothing: every grid starts from its defaults.
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = _culture;
        _context.Dispose();
    }

    private IRenderedComponent<DataGrid<Row>> Render() => _context.Render<DataGrid<Row>>(p => p
        .Add(g => g.Items, Rows)
        .Add(g => g.Columns, Columns)
        .Add(g => g.StorageKey, "test")
        .Add(g => g.DefaultSortKey, "name"));

    private static List<string> Headers(IRenderedComponent<DataGrid<Row>> grid) =>
        [.. grid.FindAll("thead th").Select(th => th.TextContent.Trim())];

    private static List<string> Names(IRenderedComponent<DataGrid<Row>> grid) =>
        [.. grid.FindAll("tbody tr").Select(tr => tr.QuerySelector("td")!.TextContent.Trim())];

    [Fact]
    public void A_hidden_column_leaves_the_table_and_the_last_one_cannot_be_hidden()
    {
        var grid = Render();

        grid.Find("button[title='Columns']").Click();
        grid.FindAll(".grid-menu-option input")[1].Change(false);

        Assert.Equal(["Name", "Date"], Headers(grid));

        grid.FindAll(".grid-menu-option input")[2].Change(false);
        var boxes = grid.FindAll(".grid-menu-option input");
        Assert.True(boxes[0].HasAttribute("disabled"));   // Name is the only one left
    }

    [Fact]
    public void A_date_range_filters_the_rows()
    {
        var grid = Render();

        grid.FindAll("button").First(b => b.TextContent.Contains("Date") && b.QuerySelector(".bi-calendar-range") is not null).Click();
        grid.FindAll("input[type=date]")[0].Change("2025-01-01");

        Assert.Equal(["Apple", "Vanguard"], Names(grid));
        Assert.Contains("–", grid.FindAll("button").First(b => b.QuerySelector(".bi-calendar-range") is not null).TextContent);
    }

    [Fact]
    public void A_saved_view_brings_its_columns_and_filters_back_after_a_reset()
    {
        var grid = Render();
        grid.Find("button[title='Columns']").Click();
        grid.FindAll(".grid-menu-option input")[1].Change(false);
        grid.Find("input[type=search]").Input("vanguard");

        grid.Find("button[title='Views']").Click();
        grid.Find("input[placeholder='View name']").Input("Mine");
        grid.FindAll("button").First(b => b.TextContent.Trim() == "Save").Click();

        grid.Find("button[title='Reset']").Click();
        Assert.Equal(["Name", "Kind", "Date"], Headers(grid));
        Assert.Equal(3, Names(grid).Count);

        grid.Find("button[title='Views']").Click();
        grid.FindAll("button").First(b => b.TextContent.Trim() == "Mine").Click();

        Assert.Equal(["Name", "Date"], Headers(grid));
        Assert.Equal(["Vanguard"], Names(grid));
    }
}
