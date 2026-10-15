using System.Globalization;
using System.Text;

namespace Arisoul.T212Ext.Web.Components.Grid;

/// <summary>
/// Filters, sorts, summarises and pages a grid's rows in memory. Kept apart from the component so
/// it can be tested. Summaries cover every filtered row, not only the page shown.
/// </summary>
public static class GridEngine
{
    public const int MaxSavedViews = 20;

    public static GridView<TItem> Apply<TItem>(IReadOnlyList<TItem> items, IReadOnlyList<GridColumn<TItem>> columns, GridState state)
    {
        IEnumerable<TItem> rows = items;
        var visible = Visible(columns, state);

        foreach (var (key, values) in state.Filters)
        {
            var column = columns.FirstOrDefault(c => c.Key == key);
            if (column is null || values.Count == 0)
                continue;

            var allowed = values.ToHashSet(StringComparer.Ordinal);
            rows = rows.Where(item => allowed.Contains(column.FilterText(item)));
        }

        foreach (var (key, range) in state.Ranges)
        {
            var column = columns.FirstOrDefault(c => c.Key == key && c.RangeFilter);
            if (column is null || range.IsEmpty)
                continue;

            rows = rows.Where(item => DateOf(column.Value(item)) is { } day
                && (range.From is null || day >= range.From) && (range.To is null || day <= range.To));
        }

        // Only what is shown is searched.
        if (!string.IsNullOrWhiteSpace(state.Search))
        {
            var terms = state.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            rows = rows.Where(item => terms.All(term =>
                visible.Any(c => c.CellText(item).Contains(term, StringComparison.CurrentCultureIgnoreCase))));
        }

        var sortColumn = columns.FirstOrDefault(c => c.Key == state.SortKey && c.Sortable);
        if (sortColumn is not null)
        {
            // Empty values always last, whatever the direction.
            var ordered = rows.OrderBy(item => sortColumn.Value(item) is null ? 1 : 0);
            rows = state.SortDescending
                ? ordered.ThenByDescending(item => sortColumn.Value(item), ValueComparer.Instance)
                : ordered.ThenBy(item => sortColumn.Value(item), ValueComparer.Instance);
        }

        var filtered = rows.ToList();
        var pageSize = Math.Max(1, state.PageSize);
        var pageCount = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
        var page = Math.Clamp(state.Page, 1, pageCount);

        return new GridView<TItem>(
            filtered,
            [.. filtered.Skip((page - 1) * pageSize).Take(pageSize)],
            page,
            pageCount,
            Summaries(filtered, columns, state));
    }

    /// <summary>The columns not hidden; all of them if every one is hidden.</summary>
    public static IReadOnlyList<GridColumn<TItem>> Visible<TItem>(IReadOnlyList<GridColumn<TItem>> columns, GridState state)
    {
        var visible = columns.Where(c => !state.Hidden.Contains(c.Key)).ToList();
        return visible.Count > 0 ? visible : columns;
    }

    /// <summary>The day of a date value, as the grids show it (UTC dates); null for anything else.</summary>
    public static DateOnly? DateOf(object? value) => value switch
    {
        DateTimeOffset dto => DateOnly.FromDateTime(dto.UtcDateTime),
        DateTime dt => DateOnly.FromDateTime(dt),
        DateOnly d => d,
        _ => null,
    };

    /// <summary>
    /// The views with <paramref name="state"/> saved as <paramref name="name"/> (a view of the same name
    /// is replaced), newest first, at most <see cref="MaxSavedViews"/>. The page is not kept.
    /// </summary>
    public static List<GridSavedView> SaveView(IReadOnlyList<GridSavedView> views, string name, GridState state)
    {
        var copy = state.Copy();
        copy.Page = 1;
        var trimmed = name.Trim();
        return [new GridSavedView { Name = trimmed, State = copy },
            .. views.Where(v => !v.Name.Equals(trimmed, StringComparison.CurrentCultureIgnoreCase)).Take(MaxSavedViews - 1)];
    }

    /// <summary>The distinct filter values of a column, over all rows.</summary>
    public static IReadOnlyList<string> FilterValues<TItem>(IReadOnlyList<TItem> items, GridColumn<TItem> column)
    {
        var values = items.Select(column.FilterText).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal);
        return column.FilterValuesDescending
            ? [.. values.OrderDescending(StringComparer.CurrentCulture)]
            : [.. values.Order(StringComparer.CurrentCulture)];
    }

    public static string DefaultText(object? value) => value switch
    {
        null => "",
        decimal d => d.ToString("N2", CultureInfo.CurrentCulture),
        DateTimeOffset dto => dto.UtcDateTime.ToString("d", CultureInfo.CurrentCulture),
        DateTime dt => dt.ToString("d", CultureInfo.CurrentCulture),
        IFormattable f => f.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>The filtered rows as CSV (all columns, values as shown), for spreadsheets.</summary>
    public static string ToCsv<TItem>(IReadOnlyList<TItem> rows, IReadOnlyList<GridColumn<TItem>> columns)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', columns.Select(c => Quote(c.Title))));
        foreach (var row in rows)
            builder.AppendLine(string.Join(';', columns.Select(c => Quote(c.CellText(row)))));
        return builder.ToString();

        // Semicolons: what spreadsheets expect where the decimal separator is a comma.
        static string Quote(string value) =>
            value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<SummaryValue>> Summaries<TItem>(IReadOnlyList<TItem> rows, IReadOnlyList<GridColumn<TItem>> columns, GridState state)
    {
        var result = new Dictionary<string, IReadOnlyList<SummaryValue>>();

        foreach (var (key, kinds) in state.Summaries)
        {
            var column = columns.FirstOrDefault(c => c.Key == key);
            if (column is null || kinds.Count == 0)
                continue;

            var values = rows.Select(column.Value).Where(v => v is not null).ToList();
            var numbers = values.Select(ToDecimal).OfType<decimal>().ToList();
            var format = column.SummaryText ?? (n => n.ToString("N2", CultureInfo.CurrentCulture));

            var summaries = new List<SummaryValue>();
            foreach (var kind in kinds.Where(column.Summaries.Contains).Order())
            {
                string? text = kind switch
                {
                    SummaryKind.Count => values.Count.ToString("N0", CultureInfo.CurrentCulture),
                    _ when numbers.Count == 0 => null,
                    SummaryKind.Sum => format(numbers.Sum()),
                    SummaryKind.Avg => format(numbers.Average()),
                    SummaryKind.Min => format(numbers.Min()),
                    SummaryKind.Max => format(numbers.Max()),
                    _ => null,
                };
                if (text is not null)
                    summaries.Add(new SummaryValue(kind, text));
            }

            if (summaries.Count > 0)
                result[key] = summaries;
        }

        return result;
    }

    private static decimal? ToDecimal(object? value) => value switch
    {
        decimal d => d,
        int i => i,
        long l => l,
        double d => (decimal)d,
        _ => null,
    };

    private sealed class ValueComparer : IComparer<object?>
    {
        public static readonly ValueComparer Instance = new();

        public int Compare(object? x, object? y) => (x, y) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            (string a, string b) => string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase),
            (IComparable a, _) when x.GetType() == y.GetType() => a.CompareTo(y),
            _ => string.Compare(x.ToString(), y.ToString(), StringComparison.CurrentCultureIgnoreCase),
        };
    }
}
