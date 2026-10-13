using Microsoft.AspNetCore.Components;

namespace Arisoul.T212Ext.Web.Components.Grid;

public enum SummaryKind
{
    Sum,
    Avg,
    Min,
    Max,
    Count,
}

/// <summary>One column of a <see cref="DataGrid{TItem}"/>.</summary>
public sealed class GridColumn<TItem>
{
    /// <summary>Stable id, used to save the grid's state. Never shown.</summary>
    public required string Key { get; init; }

    public required string Title { get; init; }

    /// <summary>The raw value: what sorting, summaries and the CSV export use.</summary>
    public required Func<TItem, object?> Value { get; init; }

    /// <summary>The text shown in the cell, searched by the search box. Defaults to <see cref="Value"/> formatted.</summary>
    public Func<TItem, string>? Text { get; init; }

    /// <summary>Custom cell content; <see cref="Text"/> is still used for search and export.</summary>
    public RenderFragment<TItem>? Template { get; init; }

    public bool Sortable { get; init; } = true;

    /// <summary>Shows a filter with the column's distinct values (by <see cref="FilterKey"/>).</summary>
    public bool Filterable { get; init; }

    /// <summary>What the filter groups by, e.g. the year of a date. Defaults to <see cref="Text"/>.</summary>
    public Func<TItem, string>? FilterKey { get; init; }

    /// <summary>Order of the filter's values: newest years first, for example.</summary>
    public bool FilterValuesDescending { get; init; }

    /// <summary>Summaries offered for this column. Empty: none.</summary>
    public IReadOnlyList<SummaryKind> Summaries { get; init; } = [];

    /// <summary>How a numeric summary is shown (e.g. as money). Defaults to N2.</summary>
    public Func<decimal, string>? SummaryText { get; init; }

    public bool AlignEnd { get; init; }

    /// <summary>Hidden on small screens (cards), still exported.</summary>
    public bool HideOnMobile { get; init; }

    /// <summary>Shown as the card title on small screens.</summary>
    public bool MobileTitle { get; init; }

    /// <summary>Hidden in the table below this Bootstrap breakpoint ("lg", "xl"); null: always shown.</summary>
    public string? HideBelow { get; init; }

    public string CellText(TItem item) => Text?.Invoke(item) ?? GridEngine.DefaultText(Value(item));

    public string FilterText(TItem item) => FilterKey?.Invoke(item) ?? CellText(item);

    public static IReadOnlyList<SummaryKind> Numeric { get; } = [SummaryKind.Sum, SummaryKind.Avg, SummaryKind.Min, SummaryKind.Max, SummaryKind.Count];
}

/// <summary>What the person chose in a grid. Saved per grid in the browser.</summary>
public sealed class GridState
{
    public string? Search { get; set; }
    public Dictionary<string, List<string>> Filters { get; set; } = [];
    public string? SortKey { get; set; }
    public bool SortDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public Dictionary<string, List<SummaryKind>> Summaries { get; set; } = [];
}

public sealed record SummaryValue(SummaryKind Kind, string Text);

public sealed record GridView<TItem>(
    IReadOnlyList<TItem> Filtered,
    IReadOnlyList<TItem> Page,
    int PageNumber,
    int PageCount,
    IReadOnlyDictionary<string, IReadOnlyList<SummaryValue>> Summaries);
