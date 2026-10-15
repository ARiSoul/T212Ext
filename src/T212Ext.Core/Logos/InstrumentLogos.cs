using System.Collections.Concurrent;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Logos;

public sealed class LogoOptions
{
    public const string Section = "Logos";

    /// <summary>Fetches logos from the public sources; off, every instrument shows its avatar.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A logo found is asked for again after this long (they rarely change).</summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(60);

    /// <summary>An instrument without a logo is asked for again after this long.</summary>
    public TimeSpan MissingRetry { get; set; } = TimeSpan.FromDays(7);
}

public sealed record LogoImage(byte[] Data, string ContentType);

/// <summary>
/// Instrument logos, fetched by the server and kept in <see cref="InstrumentLogo"/>: the browser asks
/// only this app, and each source sees one request per instrument at most every <see cref="LogoOptions.MaxAge"/>.
/// Sources, in order: Trading 212's own images (by its ticker, as its app shows them), then Parqet's
/// (by ISIN). Neither is a documented API: when both fail, the page shows a coloured avatar.
/// </summary>
public sealed class InstrumentLogos(
    IDbContextFactory<T212ExtDbContext> dbFactory,
    IHttpClientFactory httpFactory,
    IOptions<LogoOptions> options,
    TimeProvider time,
    ILogger<InstrumentLogos> logger)
{
    public const string HttpClientName = "Logos";

    /// <summary>Larger answers are not logos.</summary>
    public const int MaxBytes = 256 * 1024;

    private static readonly string[] ImageTypes = ["image/png", "image/svg+xml", "image/jpeg", "image/webp"];

    // One fetch per key at a time, however many images of it a page shows.
    private readonly ConcurrentDictionary<string, Lazy<Task<LogoImage?>>> _inFlight = new(StringComparer.Ordinal);

    /// <summary>The source addresses for an instrument, best first. Only these fixed hosts are ever asked.</summary>
    public static IReadOnlyList<(string Source, Uri Url)> Sources(string? ticker, string? isin)
    {
        var sources = new List<(string, Uri)>();
        // Trading 212 tickers look like "AAPL_US_EQ"; a CSV-only row may carry the plain symbol instead.
        if (ticker is { Length: > 0 and <= 40 } && ticker.Contains('_') && ticker.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-'))
            sources.Add(("t212", new Uri($"https://trading212equities.s3.eu-central-1.amazonaws.com/{ticker}.png")));
        if (IsIsin(isin))
            sources.Add(("parqet", new Uri($"https://assets.parqet.com/logos/isin/{isin!.ToUpperInvariant()}")));
        return sources;
    }

    public static bool IsIsin(string? isin) => isin is { Length: 12 } && isin.All(char.IsAsciiLetterOrDigit);

    public static string KeyOf(string ticker, string? isin) => IsIsin(isin) ? isin!.ToUpperInvariant() : ticker;

    /// <summary>The logo, from the database or fetched now; null when there is none (the caller shows an avatar).</summary>
    public async Task<LogoImage?> GetAsync(string ticker, string? isin, CancellationToken cancellationToken = default)
    {
        var key = KeyOf(ticker, isin);
        await using (var db = await dbFactory.CreateDbContextAsync(cancellationToken))
        {
            var stored = await db.InstrumentLogos.AsNoTracking().SingleOrDefaultAsync(l => l.Key == key, cancellationToken);
            if (stored is not null && !IsStale(stored))
                return stored.Data is { } data ? new LogoImage(data, stored.ContentType!) : null;
            if (!options.Value.Enabled)
                return stored?.Data is { } old ? new LogoImage(old, stored.ContentType!) : null;
        }

        var fetch = _inFlight.GetOrAdd(key, _ => new Lazy<Task<LogoImage?>>(() => FetchAndStoreAsync(key, ticker, isin)));
        try
        {
            return await fetch.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<LogoImage?>>>(key, fetch));
        }
    }

    private bool IsStale(InstrumentLogo logo) =>
        time.GetUtcNow() - logo.FetchedAt > (logo.Data is null ? options.Value.MissingRetry : options.Value.MaxAge);

    private async Task<LogoImage?> FetchAndStoreAsync(string key, string ticker, string? isin)
    {
        LogoImage? found = null;
        string? source = null;
        var http = httpFactory.CreateClient(HttpClientName);

        foreach (var (name, url) in Sources(ticker, isin))
        {
            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                var type = response.Content.Headers.ContentType?.MediaType;
                if (!response.IsSuccessStatusCode || type is null || !ImageTypes.Contains(type) || response.Content.Headers.ContentLength > MaxBytes)
                    continue;

                var data = await response.Content.ReadAsByteArrayAsync();
                if (data.Length is 0 or > MaxBytes)
                    continue;

                (found, source) = (new LogoImage(data, type), name);
                break;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogDebug(ex, "Logo source {Source} failed for {Key}", name, key);
            }
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var logo = await db.InstrumentLogos.SingleOrDefaultAsync(l => l.Key == key);
        if (logo is null)
        {
            logo = new InstrumentLogo { Key = key };
            db.InstrumentLogos.Add(logo);
        }
        // A source that fails now does not erase a logo found before.
        if (found is not null || logo.Data is null)
            (logo.Data, logo.ContentType, logo.Source) = (found?.Data, found?.ContentType, source);
        logo.FetchedAt = time.GetUtcNow();
        await db.SaveChangesAsync();

        return logo.Data is { } stored ? new LogoImage(stored, logo.ContentType!) : null;
    }
}
