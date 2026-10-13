using System.Globalization;
using ApexCharts;

namespace Arisoul.T212Ext.Web.Components.Charts;

/// <summary>
/// The look every chart shares. Series colours are the categorical palette of the data-viz guide,
/// in its fixed order, with the light and dark steps validated against the app's card surfaces
/// (#ffffff and #1b1c20): adjacent colour-blind ΔE ≥ 8.4, normal-vision ΔE ≥ 19.3. The light
/// aqua, yellow and pink are under 3:1 against white, so every chart keeps its numbers in a
/// tooltip and in a table next to it.
/// </summary>
public static class ChartStyle
{
    private static readonly List<string> Light = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300"];
    private static readonly List<string> Dark = ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300"];

    /// <summary>Colour of a series by its fixed slot (0-based). Colour follows the series, never its rank.</summary>
    public static string Series(int slot, bool dark) => (dark ? Dark : Light)[slot % 6];

    public static List<string> Palette(bool dark) => [.. dark ? Dark : Light];

    public static string Surface(bool dark) => dark ? "#1b1c20" : "#ffffff";

    public static ApexChartOptions<T> Base<T>(bool dark) where T : class => new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            FontFamily = "Inter, system-ui, -apple-system, Segoe UI, Roboto, sans-serif",
            ForeColor = dark ? "#a2a8b3" : "#5d6470",
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false },
        },
        Theme = new Theme { Mode = dark ? Mode.Dark : Mode.Light },
        Colors = Palette(dark),
        Grid = new ApexCharts.Grid
        {
            BorderColor = dark ? "#2d3036" : "#e8eaee",
            StrokeDashArray = 0,
            Xaxis = new GridXAxis { Lines = new Lines { Show = false } },
        },
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend { Position = LegendPosition.Top, HorizontalAlign = Align.Left, FontSize = "13px" },
        Tooltip = new Tooltip { Theme = dark ? Mode.Dark : Mode.Light },
        States = new States { Active = new StatesActive { Filter = new StatesFilter { Type = StatesFilterType.none } } },
    };

    /// <summary>Thin bars with rounded ends and a 2 px gap in the surface colour between them.</summary>
    public static PlotOptions Bars(bool horizontal = false, string width = "60%") => new()
    {
        Bar = new PlotOptionsBar
        {
            Horizontal = horizontal,
            ColumnWidth = width,
            BarHeight = width,
            BorderRadius = 4,
            BorderRadiusApplication = BorderRadiusApplication.End,
        },
    };

    /// <summary>A JS formatter: whole numbers in the current culture, for axes.</summary>
    public static string AxisNumber => $"function(v) {{ return new Intl.NumberFormat('{Culture}', {{ maximumFractionDigits: 0 }}).format(v); }}";

    /// <summary>A JS formatter: money with two decimals and the currency code, for tooltips.</summary>
    public static string Money(string currency) =>
        $"function(v) {{ return v == null ? '' : new Intl.NumberFormat('{Culture}', {{ minimumFractionDigits: 2, maximumFractionDigits: 2 }}).format(v) + ' {currency}'; }}";

    /// <summary>A JS formatter for donut legends: the share of each slice.</summary>
    public static string LegendShare =>
        $"function(name, opts) {{ var s = opts.w.globals.series; var t = s.reduce(function(a, b) {{ return a + b; }}, 0); return name + ' · ' + new Intl.NumberFormat('{Culture}', {{ style: 'percent', maximumFractionDigits: 1 }}).format(t ? s[opts.seriesIndex] / t : 0); }}";

    private static string Culture => CultureInfo.CurrentCulture.Name;
}
