using Arisoul.T212Ext.Core.Import;

namespace Arisoul.T212Ext.Core.Tests;

public class T212CsvParserTests
{
    /// <summary>The current layout: the currency of each amount has its own column.</summary>
    internal const string NewLayout = """
        Action,Time,ISIN,Ticker,Name,Notes,ID,No. of shares,Price / share,Currency (Price / share),Exchange rate,Total,Currency (Total),Withholding tax,Currency (Withholding tax),Currency conversion fee,Currency (Currency conversion fee)
        Deposit,2025-01-02 09:15:00,,,,,"dep-1",,,,,1000.00,"EUR",,,,
        Market buy,2025-01-03 14:30:01.123,US0378331005,AAPL,"Apple Inc.",,EOF123,2.5,240.10,USD,1.0350,580.17,"EUR",,,0.87,"EUR"
        Dividend (Ordinary),2025-02-13 10:01:02,US0378331005,AAPL,"Apple Inc.",,,2.5,0.25,USD,"Not available",0.51,"EUR",0.09,USD,,
        Dividend (Return of capital),2025-02-13 10:01:02,US0378331005,AAPL,"Apple Inc.",,,2.5,0.10,USD,"Not available",0.24,"EUR",,,,
        Interest on cash,2025-02-28 23:59:59,,,,,"int-1",,,,,1.23,"EUR",,,,
        Currency conversion,2025-03-01 08:00:00,,,,"Converted 100 EUR, got 108 USD",cc-1,,,,,100.00,"EUR",,,,
        "Dividend (Dividends paid by us corporations)",2025-03-15 09:00:00,US5949181045,MSFT,"Microsoft ""MSFT"" Corporation, Inc.",,,1,0.83,USD,"Not available",0.65,"EUR",0.12,USD,,
        Withdrawal,2025-04-01 10:00:00,,,,,"wd-1",,,,,-200.00,"EUR",,,,
        """;

    /// <summary>
    /// The layout of reports exported in October 2026 (header copied from real reports, values made up):
    /// "Time (UTC)" with an offset, and "Dividend adjustment" rows for withholding tax refunds.
    /// </summary>
    internal const string Layout2026 = """
        Action,Time (UTC),ISIN,Ticker,Name,Notes,ID,No. of shares,Price / share,Currency (Price / share),Exchange rate,Result,Currency (Result),Total,Currency (Total),Withholding tax,Currency (Withholding tax),Charge amount,Currency (Charge amount),Deposit fee,Currency (Deposit fee),Currency conversion fee,Currency (Currency conversion fee)
        Deposit,2025-01-01 03:56:57+00:00,,,,,"dep-2026-1",,,,,,,500.00,"EUR",,,,,,,,
        Market buy,2025-01-02 14:30:01+00:00,IE00BK5BQT80,VWCE,"Vanguard FTSE All-World",,EOF999,1.5,120.00,EUR,1.00,,,180.00,"EUR",,,,,,,,
        Dividend (Dividend),2025-03-27 09:12:44+00:00,US0378331005,AAPL,"Apple",,,4.2,0.25,USD,"Not available",,,0.82,"EUR",0.16,USD,,,,,,
        Dividend (Dividend manufactured payment),2025-04-10 09:00:00+00:00,US5949181045,MSFT,"Microsoft",,,1,0.83,USD,"Not available",,,0.65,"EUR",0.12,USD,,,,,,
        Lending interest,2025-04-30 23:00:00+00:00,,,,,"li-1",,,,,,,0.03,"EUR",,,,,,,,
        Dividend adjustment,2025-06-13 11:07:15+00:00,,,,"2024 US dividends withholding tax adjustment",adj-1,,,,,,,1.27,"EUR",,,,,,,,
        """;

    /// <summary>The older layout: the currency is in the header ("Total (EUR)").</summary>
    internal const string OldLayout = """
        Action,Time,ISIN,Ticker,Name,No. of shares,Price / share,Currency (Price / share),Exchange rate,Result (EUR),Total (EUR),Withholding tax,Currency (Withholding tax),Charge amount (EUR),Notes,ID
        Dividend (Ordinary),2021-06-10 08:00:00,US0378331005,AAPL,Apple,3,0.22,USD,Not available,,0.48,0.10,USD,,,
        """;

    private static T212CsvParseResult Parse(string csv) => T212CsvParser.Parse(new StringReader(csv));

    [Fact]
    public void Reads_the_current_layout()
    {
        var result = Parse(NewLayout);

        Assert.Empty(result.Errors);
        Assert.Equal(8, result.Rows.Count);

        var dividend = result.Rows[2];
        Assert.Equal("Dividend (Ordinary)", dividend.Action);
        Assert.Equal(new DateTimeOffset(2025, 2, 13, 10, 1, 2, TimeSpan.Zero), dividend.Time);
        Assert.Equal("US0378331005", dividend.Isin);
        Assert.Equal(2.5m, dividend.Shares);
        Assert.Equal(0.25m, dividend.PricePerShare);
        Assert.Equal("USD", dividend.PriceCurrency);
        Assert.Null(dividend.ExchangeRate);
        Assert.Equal(0.51m, dividend.Total);
        Assert.Equal("EUR", dividend.TotalCurrency);
        Assert.Equal(0.09m, dividend.WithholdingTax);
        Assert.Equal("USD", dividend.WithholdingTaxCurrency);
        Assert.Null(dividend.Id);

        Assert.Equal("dep-1", result.Rows[0].Id);
        Assert.Equal(new DateTimeOffset(2025, 1, 3, 14, 30, 1, 123, TimeSpan.Zero), result.Rows[1].Time);
        Assert.Equal(-200m, result.Rows[7].Total);
    }

    [Fact]
    public void Reads_the_2026_layout_with_time_in_utc_and_an_offset()
    {
        var result = T212CsvParser.Parse(new StringReader(Layout2026));

        Assert.Empty(result.Errors);
        Assert.Equal(6, result.Rows.Count);
        Assert.Equal(new DateTimeOffset(2025, 1, 1, 3, 56, 57, TimeSpan.Zero), result.Rows[0].Time);
        var dividend = result.Rows[2];
        Assert.Equal(new DateTimeOffset(2025, 3, 27, 9, 12, 44, TimeSpan.Zero), dividend.Time);
        Assert.Equal(0.82m, dividend.Total);
        Assert.Equal(0.16m, dividend.WithholdingTax);
        Assert.Equal("2024 US dividends withholding tax adjustment", result.Rows[5].Notes);
    }

    [Fact]
    public void Times_with_another_offset_are_converted_to_utc()
    {
        var row = Assert.Single(Parse("Action,Time (UTC),Total\nDeposit,2025-07-01 10:00:00+01:00,10\n").Rows);

        Assert.Equal(new DateTimeOffset(2025, 7, 1, 9, 0, 0, TimeSpan.Zero), row.Time);
        Assert.Equal(TimeSpan.Zero, row.Time.Offset);
    }

    [Fact]
    public void Reads_the_older_layout_with_the_currency_in_the_header()
    {
        var row = Assert.Single(Parse(OldLayout).Rows);

        Assert.Equal(0.48m, row.Total);
        Assert.Equal("EUR", row.TotalCurrency);
        Assert.Equal(0.10m, row.WithholdingTax);
        Assert.Equal("USD", row.WithholdingTaxCurrency);
        Assert.Equal(3m, row.Shares);
    }

    [Fact]
    public void Quoted_fields_keep_commas_and_doubled_quotes()
    {
        var rows = Parse(NewLayout).Rows;

        Assert.Equal("Converted 100 EUR, got 108 USD", rows[5].Notes);
        Assert.Equal("Microsoft \"MSFT\" Corporation, Inc.", rows[6].Name);
        Assert.Equal("Dividend (Dividends paid by us corporations)", rows[6].Action);
    }

    [Fact]
    public void Copes_with_a_byte_order_mark_and_windows_line_ends()
    {
        var csv = "﻿" + OldLayout.Replace("\n", "\r\n");

        var row = Assert.Single(Parse(csv).Rows);

        Assert.Equal("Dividend (Ordinary)", row.Action);
        Assert.Equal(0.48m, row.Total);
    }

    [Fact]
    public void Numbers_do_not_depend_on_the_server_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-PT");
        try
        {
            Assert.Equal(0.48m, Assert.Single(Parse(OldLayout).Rows).Total);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_line_with_an_unreadable_time_is_reported_and_skipped()
    {
        var csv = "Action,Time,Total\nDeposit,yesterday,10\nDeposit,2025-01-01 10:00:00,20\n";

        var result = Parse(csv);

        Assert.Equal(20m, Assert.Single(result.Rows).Total);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("yesterday", error.Message);
    }

    [Fact]
    public void A_file_that_is_not_a_report_is_refused()
    {
        var error = Assert.Throws<FormatException>(() => Parse("Date,Amount\n2025-01-01,10\n"));

        Assert.Contains("Action", error.Message);
    }

    [Theory]
    [InlineData("Dividend (Ordinary)", "Ordinary")]
    [InlineData("Dividend (Dividends paid by us corporations)", "Dividends paid by us corporations")]
    [InlineData("Dividend", "Dividend")]
    public void Dividend_type_is_the_text_in_brackets(string action, string expected) =>
        Assert.Equal(expected, CsvImportService.DividendType(action));
}
