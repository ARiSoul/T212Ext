using System.Globalization;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arisoul.T212Ext.Core.Rates;

/// <summary>Daily euro reference rates: units of the currency per 1 EUR, one per business day.</summary>
public interface IExchangeRateSource
{
    Task<IReadOnlyList<(DateOnly Date, decimal PerEuro)>> GetDailyRatesAsync(string currency, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>
/// The European Central Bank's euro reference rates, from its data portal (series EXR.D.{currency}.EUR.SP00.A).
/// There are no rates on weekends and TARGET holidays.
/// </summary>
public sealed class EcbRateSource(IHttpClientFactory httpClientFactory) : IExchangeRateSource
{
    public const string HttpClientName = "ECB";

    public async Task<IReadOnlyList<(DateOnly Date, decimal PerEuro)>> GetDailyRatesAsync(string currency, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
            throw new ArgumentException($"Not a currency code: {currency}.", nameof(currency));

        var url = $"https://data-api.ecb.europa.eu/service/data/EXR/D.{currency}.EUR.SP00.A"
            + $"?startPeriod={from:yyyy-MM-dd}&endPeriod={to:yyyy-MM-dd}&format=csvdata";

        using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync(url, cancellationToken);

        // The ECB answers 404 when the period has no observations (e.g. a weekend only).
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    /// <summary>Reads the TIME_PERIOD and OBS_VALUE columns of an ECB "csvdata" answer.</summary>
    /// <remarks>
    /// A plain split is enough: the only quoted fields (titles with commas) come after both columns.
    /// </remarks>
    internal static IReadOnlyList<(DateOnly Date, decimal PerEuro)> Parse(string csv)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
            return [];

        var header = lines[0].Split(',');
        var dateColumn = Array.IndexOf(header, "TIME_PERIOD");
        var valueColumn = Array.IndexOf(header, "OBS_VALUE");
        if (dateColumn < 0 || valueColumn < 0)
            throw new FormatException("The ECB answer has no TIME_PERIOD and OBS_VALUE columns.");

        var rates = new List<(DateOnly, decimal)>();
        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split(',');
            if (fields.Length <= Math.Max(dateColumn, valueColumn))
                continue;

            if (DateOnly.TryParseExact(fields[dateColumn], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && decimal.TryParse(fields[valueColumn], NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                && value > 0)
                rates.Add((date, value));
        }

        return rates;
    }
}

/// <summary>Keeps the stored rates of every currency the dividends need, from the first dividend on.</summary>
public sealed class ExchangeRateUpdater(
    IDbContextFactory<T212ExtDbContext> dbFactory,
    IExchangeRateSource source,
    TimeProvider time,
    ILogger<ExchangeRateUpdater> logger)
{
    /// <summary>Fetches what is missing; returns how many rates were added.</summary>
    public async Task<int> UpdateAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var currencies = (await db.Dividends.Select(d => d.InstrumentCurrency).Distinct().ToListAsync(cancellationToken))
            .Concat(await db.Dividends.Select(d => d.WithholdingTaxCurrency).Distinct().ToListAsync(cancellationToken))
            .Concat(await db.Dividends.Select(d => d.Currency).Distinct().ToListAsync(cancellationToken))
            .Concat(await db.Orders.Where(o => o.FillId != null).Select(o => o.InstrumentCurrency).Distinct().ToListAsync(cancellationToken))
            .OfType<string>()
            .Select(c => RateTable.Normalise(0, c.Trim()).Currency)
            .Where(c => c.Length == 3 && c != "EUR")
            .Distinct()
            .Order()
            .ToList();

        if (currencies.Count == 0)
            return 0;

        var firstDividend = await db.Dividends.MinAsync(d => (DateTimeOffset?)d.PaidOn, cancellationToken);
        var firstTrade = await db.Orders.Where(o => o.FillId != null).MinAsync(o => o.FilledAt, cancellationToken);
        var first = new[] { firstDividend, firstTrade }.Where(d => d is not null).Min();
        if (first is null)
            return 0;

        // A week before the first payment or trade, so a Monday after a holiday still has a rate.
        var start = DateOnly.FromDateTime(first.Value.UtcDateTime).AddDays(-7);
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var added = 0;

        foreach (var currency in currencies)
        {
            var last = await db.ExchangeRates.Where(r => r.Currency == currency).MaxAsync(r => (DateOnly?)r.Date, cancellationToken);
            var from = last is { } known && known >= start ? known.AddDays(1) : start;
            if (from > today)
                continue;

            var existing = (await db.ExchangeRates.Where(r => r.Currency == currency && r.Date >= from).Select(r => r.Date).ToListAsync(cancellationToken)).ToHashSet();
            var rates = await source.GetDailyRatesAsync(currency, from, today, cancellationToken);
            foreach (var (date, perEuro) in rates.Where(r => !existing.Contains(r.Date)))
            {
                db.ExchangeRates.Add(new ExchangeRate { Currency = currency, Date = date, PerEuro = perEuro });
                added++;
            }

            logger.LogInformation("Exchange rates {Currency}: {Count} new from {From}", currency, rates.Count, from);
        }

        await db.SaveChangesAsync(cancellationToken);
        return added;
    }
}
