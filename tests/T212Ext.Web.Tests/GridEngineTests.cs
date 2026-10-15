using System.Globalization;
using Arisoul.T212Ext.Web.Components.Grid;

namespace Arisoul.T212Ext.Web.Tests;

public class GridEngineTests
{
    private sealed record Row(string Name, string Kind, decimal? Amount, DateTimeOffset Date);

    private static readonly IReadOnlyList<Row> Rows =
    [
        new("Apple", "Stock", 10m, new(2025, 3, 1, 0, 0, 0, TimeSpan.Zero)),
        new("Vanguard All-World", "ETF", 30m, new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        new("Microsoft", "Stock", null, new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)),
        new("Realty Income", "Stock", 20m, new(2024, 7, 1, 0, 0, 0, TimeSpan.Zero)),
    ];

    private static readonly IReadOnlyList<GridColumn<Row>> Columns =
    [
        new() { Key = "name", Title = "Name", Value = r => r.Name },
        new() { Key = "kind", Title = "Kind", Value = r => r.Kind, Filterable = true },
        new() { Key = "amount", Title = "Amount", Value = r => r.Amount, Summaries = GridColumn<Row>.Numeric, SummaryText = n => n.ToString("0.00", CultureInfo.InvariantCulture) },
        new() { Key = "date", Title = "Date", Value = r => r.Date, Filterable = true, FilterKey = r => r.Date.Year.ToString(), FilterValuesDescending = true },
    ];

    private static GridView<Row> Apply(GridState state) => GridEngine.Apply(Rows, Columns, state);

    [Fact]
    public void Search_matches_every_word_in_any_column()
    {
        Assert.Equal(["Vanguard All-World"], Apply(new GridState { Search = "etf world" }).Filtered.Select(r => r.Name));
        Assert.Equal(3, Apply(new GridState { Search = "stock" }).Filtered.Count);
        Assert.Empty(Apply(new GridState { Search = "stock world" }).Filtered);
    }

    [Fact]
    public void Filters_keep_rows_with_any_chosen_value_and_combine_across_columns()
    {
        var state = new GridState
        {
            Filters = new() { ["date"] = ["2025", "2026"], ["kind"] = ["Stock"] },
        };

        Assert.Equal(["Apple", "Microsoft"], Apply(state).Filtered.Select(r => r.Name));
    }

    [Fact]
    public void Filter_values_are_distinct_and_ordered()
    {
        Assert.Equal(["2026", "2025", "2024"], GridEngine.FilterValues(Rows, Columns[3]));
        Assert.Equal(["ETF", "Stock"], GridEngine.FilterValues(Rows, Columns[1]));
    }

    [Fact]
    public void Sorting_keeps_empty_values_last_in_both_directions()
    {
        Assert.Equal([10m, 20m, 30m, null], Apply(new GridState { SortKey = "amount" }).Filtered.Select(r => r.Amount));
        Assert.Equal([30m, 20m, 10m, null], Apply(new GridState { SortKey = "amount", SortDescending = true }).Filtered.Select(r => r.Amount));
    }

    [Fact]
    public void Sorting_dates_uses_the_value_not_the_text()
    {
        var names = Apply(new GridState { SortKey = "date", SortDescending = true }).Filtered.Select(r => r.Name);

        Assert.Equal(["Microsoft", "Vanguard All-World", "Apple", "Realty Income"], names);
    }

    [Fact]
    public void Paging_clamps_to_the_last_page()
    {
        var view = Apply(new GridState { PageSize = 3, Page = 9, SortKey = "name" });

        Assert.Equal(2, view.PageCount);
        Assert.Equal(2, view.PageNumber);
        Assert.Equal(["Vanguard All-World"], view.Page.Select(r => r.Name));
    }

    [Fact]
    public void Summaries_cover_every_filtered_row_not_only_the_page()
    {
        var view = Apply(new GridState
        {
            PageSize = 1,
            Filters = new() { ["kind"] = ["Stock"] },
            Summaries = new() { ["amount"] = [SummaryKind.Sum, SummaryKind.Avg, SummaryKind.Count, SummaryKind.Max] },
        });

        Assert.Single(view.Page);
        var summaries = view.Summaries["amount"].ToDictionary(s => s.Kind, s => s.Text);
        Assert.Equal("30.00", summaries[SummaryKind.Sum]);
        Assert.Equal("15.00", summaries[SummaryKind.Avg]);   // empty amounts are left out, not counted as zero
        Assert.Equal("20.00", summaries[SummaryKind.Max]);
        Assert.Equal("2", summaries[SummaryKind.Count]);
    }

    [Fact]
    public void Summaries_a_column_does_not_offer_are_ignored()
    {
        var view = Apply(new GridState { Summaries = new() { ["name"] = [SummaryKind.Sum] } });

        Assert.Empty(view.Summaries);
    }

    [Fact]
    public void Csv_export_quotes_what_needs_quoting()
    {
        IReadOnlyList<GridColumn<Row>> columns =
        [
            new() { Key = "name", Title = "Name", Value = r => r.Name },
            new() { Key = "note", Title = "Note", Value = r => "a;b \"c\"" },
        ];

        var csv = GridEngine.ToCsv([Rows[0]], columns);

        Assert.Equal("Name;Note" + Environment.NewLine + "Apple;\"a;b \"\"c\"\"\"" + Environment.NewLine, csv);
    }

    private static readonly IReadOnlyList<GridColumn<Row>> RangeColumns =
    [
        new() { Key = "name", Title = "Name", Value = r => r.Name },
        new() { Key = "kind", Title = "Kind", Value = r => r.Kind },
        new() { Key = "date", Title = "Date", Value = r => r.Date, RangeFilter = true },
    ];

    [Fact]
    public void A_date_range_keeps_both_ends_and_either_can_be_open()
    {
        GridState Range(DateOnly? from, DateOnly? to) => new() { Ranges = new() { ["date"] = new DateRange { From = from, To = to } } };
        IEnumerable<string> Names(GridState state) => GridEngine.Apply(Rows, RangeColumns, state).Filtered.Select(r => r.Name).Order();

        Assert.Equal(["Apple", "Vanguard All-World"], Names(Range(new(2025, 3, 1), new(2026, 1, 1))));
        Assert.Equal(["Microsoft", "Vanguard All-World"], Names(Range(new(2025, 3, 2), null)));
        Assert.Equal(["Realty Income"], Names(Range(null, new(2025, 2, 28))));
    }

    [Fact]
    public void Hidden_columns_are_not_searched_and_at_least_one_stays_visible()
    {
        var hidden = new GridState { Hidden = ["kind"], Search = "etf" };

        Assert.Empty(GridEngine.Apply(Rows, RangeColumns, hidden).Filtered);
        Assert.Equal(["name", "date"], GridEngine.Visible(RangeColumns, hidden).Select(c => c.Key));
        Assert.Equal(3, GridEngine.Visible(RangeColumns, new GridState { Hidden = ["name", "kind", "date"] }).Count);
    }

    [Fact]
    public void Saved_views_replace_one_of_the_same_name_newest_first_and_are_copies()
    {
        var state = new GridState { Search = "apple", Page = 3, Hidden = ["kind"] };

        var views = GridEngine.SaveView([], "Mine", state);
        views = GridEngine.SaveView(views, "Other", new GridState());
        views = GridEngine.SaveView(views, " mine ", new GridState { Search = "msft" });
        state.Hidden.Add("date");

        Assert.Equal(["mine", "Other"], views.Select(v => v.Name));
        Assert.Equal("msft", views[0].State.Search);
        Assert.Equal(1, GridEngine.SaveView([], "x", state)[0].State.Page);   // a view does not keep the page

        var first = GridEngine.SaveView([], "Mine", new GridState { Hidden = ["kind"] });
        var copy = first[0].State.Copy();
        copy.Hidden.Add("date");
        Assert.Equal(["kind"], first[0].State.Hidden);
    }

    [Fact]
    public void At_most_twenty_views_are_kept()
    {
        List<GridSavedView> views = [];
        for (var i = 0; i < 25; i++)
            views = GridEngine.SaveView(views, $"v{i}", new GridState());

        Assert.Equal(GridEngine.MaxSavedViews, views.Count);
        Assert.Equal("v24", views[0].Name);
    }

    [Theory]
    [InlineData("2026-01-31T23:30:00-02:00", "2026-02-01")]
    [InlineData("2026-01-31T23:30:00+00:00", "2026-01-31")]
    public void Dates_are_filtered_by_their_utc_day_as_the_grids_show_them(string value, string day) =>
        Assert.Equal(DateOnly.Parse(day), GridEngine.DateOf(DateTimeOffset.Parse(value)));
}
