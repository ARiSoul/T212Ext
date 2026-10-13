using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Arisoul.T212Ext.Core.Import;

/// <summary>One line of a Trading 212 CSV report ("History → Export" in the app, or <c>/history/exports</c>).</summary>
public sealed record T212CsvRow(
    int LineNumber,
    string Action,
    DateTimeOffset Time,
    string? Isin,
    string? Ticker,
    string? Name,
    string? Id,
    decimal? Shares,
    decimal? PricePerShare,
    string? PriceCurrency,
    decimal? ExchangeRate,
    decimal? Total,
    string? TotalCurrency,
    decimal? WithholdingTax,
    string? WithholdingTaxCurrency,
    string? Notes);

public sealed record T212CsvError(int LineNumber, string Message);

public sealed record T212CsvParseResult(IReadOnlyList<T212CsvRow> Rows, IReadOnlyList<T212CsvError> Errors);

/// <summary>
/// Reads Trading 212 CSV reports by column name, so it copes with the columns that come and go
/// between reports (a report only has the columns its rows use).
/// </summary>
/// <remarks>
/// Two layouts exist. Newer reports put the currency in its own column ("Total", "Currency (Total)");
/// older ones put it in the header ("Total (EUR)"). The time column is "Time" or "Time (UTC)"; times
/// carry an offset ("2025-01-01 03:56:57+00:00") or none, in which case they are read as UTC.
/// </remarks>
public static partial class T212CsvParser
{
    private static readonly string[] TimeFormats =
    [
        "yyyy-MM-dd HH:mm:sszzz",
        "yyyy-MM-dd HH:mm:ss.fffzzz",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ssZ",
    ];

    public static T212CsvParseResult Parse(TextReader reader)
    {
        var records = ReadRecords(reader).GetEnumerator();
        if (!records.MoveNext())
            throw new FormatException("The file is empty.");

        var header = records.Current.Fields.Select(h => h.Trim().TrimStart('﻿')).ToList();
        var columns = new Columns(header);
        if (columns.Action < 0 || columns.Time < 0)
            throw new FormatException("This is not a Trading 212 CSV report: the \"Action\" and \"Time\" columns are missing.");

        var rows = new List<T212CsvRow>();
        var errors = new List<T212CsvError>();

        while (records.MoveNext())
        {
            var (line, fields) = records.Current;
            if (fields.All(string.IsNullOrWhiteSpace))
                continue;

            var action = Field(fields, columns.Action);
            if (string.IsNullOrWhiteSpace(action))
            {
                errors.Add(new T212CsvError(line, "No action."));
                continue;
            }

            if (!TryParseTime(Field(fields, columns.Time), out var time))
            {
                errors.Add(new T212CsvError(line, $"Unreadable time \"{Field(fields, columns.Time)}\"."));
                continue;
            }

            rows.Add(new T212CsvRow(
                line,
                action.Trim(),
                time,
                Text(fields, columns.Isin),
                Text(fields, columns.Ticker),
                Text(fields, columns.Name),
                Text(fields, columns.Id),
                Number(fields, columns.Shares),
                Number(fields, columns.PricePerShare),
                Text(fields, columns.PriceCurrency),
                Number(fields, columns.ExchangeRate),
                Number(fields, columns.Total.Index),
                Text(fields, columns.TotalCurrency) ?? columns.Total.HeaderCurrency,
                Number(fields, columns.WithholdingTax.Index),
                Text(fields, columns.WithholdingTaxCurrency) ?? columns.WithholdingTax.HeaderCurrency,
                Text(fields, columns.Notes)));
        }

        return new T212CsvParseResult(rows, errors);
    }

    private static string Field(IReadOnlyList<string> fields, int index) =>
        index >= 0 && index < fields.Count ? fields[index] : "";

    private static string? Text(IReadOnlyList<string> fields, int index)
    {
        var value = Field(fields, index).Trim();
        return value.Length == 0 || value.Equals("Not available", StringComparison.OrdinalIgnoreCase) ? null : value;
    }

    private static decimal? Number(IReadOnlyList<string> fields, int index)
    {
        var value = Text(fields, index);
        return value is not null && decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static bool TryParseTime(string value, out DateTimeOffset time)
    {
        value = value.Trim();
        if (DateTimeOffset.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out time)
            || DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out time))
        {
            time = time.ToUniversalTime();
            return true;
        }

        return false;
    }

    /// <summary>RFC 4180: quoted fields may hold commas, doubled quotes and line breaks.</summary>
    private static IEnumerable<(int Line, List<string> Fields)> ReadRecords(TextReader reader)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;
        int c;

        while ((c = reader.Read()) != -1)
        {
            var ch = (char)c;

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        field.Append('"');
                        reader.Read();
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (ch == '\n')
                        line++;
                    field.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    yield return (recordLine, fields);
                    fields = [];
                    line++;
                    recordLine = line;
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            yield return (recordLine, fields);
        }
    }

    [GeneratedRegex(@"^(?<name>.+?)\s*\((?<currency>[A-Z]{3})\)$")]
    private static partial Regex CurrencyInHeader();

    private readonly record struct AmountColumn(int Index, string? HeaderCurrency);

    private sealed class Columns
    {
        public Columns(IReadOnlyList<string> header)
        {
            int Find(string name) => header.ToList().FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));

            AmountColumn FindAmount(string name)
            {
                var exact = Find(name);
                if (exact >= 0)
                    return new AmountColumn(exact, null);

                for (var i = 0; i < header.Count; i++)
                {
                    var match = CurrencyInHeader().Match(header[i]);
                    if (match.Success && match.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return new AmountColumn(i, match.Groups["currency"].Value);
                }

                return new AmountColumn(-1, null);
            }

            Action = Find("Action");
            Time = Find("Time");
            if (Time < 0)
                Time = header.ToList().FindIndex(h => h.StartsWith("Time (", StringComparison.OrdinalIgnoreCase));
            Isin = Find("ISIN");
            Ticker = Find("Ticker");
            Name = Find("Name");
            Id = Find("ID");
            Notes = Find("Notes");
            Shares = Find("No. of shares");
            PricePerShare = Find("Price / share");
            PriceCurrency = Find("Currency (Price / share)");
            ExchangeRate = Find("Exchange rate");
            Total = FindAmount("Total");
            TotalCurrency = Find("Currency (Total)");
            WithholdingTax = FindAmount("Withholding tax");
            WithholdingTaxCurrency = Find("Currency (Withholding tax)");
        }

        public int Action { get; }
        public int Time { get; }
        public int Isin { get; }
        public int Ticker { get; }
        public int Name { get; }
        public int Id { get; }
        public int Notes { get; }
        public int Shares { get; }
        public int PricePerShare { get; }
        public int PriceCurrency { get; }
        public int ExchangeRate { get; }
        public AmountColumn Total { get; }
        public int TotalCurrency { get; }
        public AmountColumn WithholdingTax { get; }
        public int WithholdingTaxCurrency { get; }
    }
}
