using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Arisoul.T212Ext.Client.Models;
using Arisoul.T212Ext.Client.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Arisoul.T212Ext.Client;

/// <summary>
/// <see cref="IT212Api"/> over HTTP for one account. Create it through <see cref="IT212ApiFactory"/>,
/// which keeps one rate limiter per account.
/// </summary>
public sealed class T212ApiClient(
    HttpClient http,
    T212Account account,
    T212RateLimiter rateLimiter,
    IDelayer delayer,
    ILogger<T212ApiClient> logger) : IT212Api
{
    /// <summary>The largest page the history endpoints accept.</summary>
    public const int PageSize = 50;

    internal const int MaxRateLimitRetries = 5;
    internal const int MaxTransientRetries = 3;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<AccountSummary> GetAccountSummaryAsync(CancellationToken cancellationToken = default) =>
        SendAsync<AccountSummary>(HttpMethod.Get, T212Endpoint.AccountSummary, "", null, cancellationToken);

    public async Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken cancellationToken = default) =>
        await SendAsync<List<Position>>(HttpMethod.Get, T212Endpoint.Positions, "", null, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<TradableInstrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default) =>
        await SendAsync<List<TradableInstrument>>(HttpMethod.Get, T212Endpoint.Instruments, "", null, cancellationToken).ConfigureAwait(false);

    public IAsyncEnumerable<T212Page<HistoryDividendItem>> GetDividendPagesAsync(string? ticker = null, CancellationToken cancellationToken = default) =>
        GetPagesAsync<HistoryDividendItem>(T212Endpoint.Dividends, Query(("limit", PageSize.ToString()), ("ticker", ticker)), cancellationToken);

    public IAsyncEnumerable<T212Page<HistoricalOrder>> GetOrderPagesAsync(string? ticker = null, CancellationToken cancellationToken = default) =>
        GetPagesAsync<HistoricalOrder>(T212Endpoint.Orders, Query(("limit", PageSize.ToString()), ("ticker", ticker)), cancellationToken);

    public IAsyncEnumerable<T212Page<HistoryTransactionItem>> GetTransactionPagesAsync(DateTimeOffset? from = null, CancellationToken cancellationToken = default) =>
        GetPagesAsync<HistoryTransactionItem>(T212Endpoint.Transactions, Query(("limit", PageSize.ToString()), ("time", from?.UtcDateTime.ToString("O"))), cancellationToken);

    public async Task<long> RequestReportAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        var enqueued = await SendAsync<EnqueuedReport>(HttpMethod.Post, T212Endpoint.RequestReport, "", request, cancellationToken).ConfigureAwait(false);
        return enqueued.ReportId ?? throw new T212ApiException("Trading 212 accepted the report request but returned no report id.", endpoint: T212Endpoint.RequestReport.Path);
    }

    public async Task<IReadOnlyList<ReportStatus>> GetReportsAsync(CancellationToken cancellationToken = default) =>
        await SendAsync<List<ReportStatus>>(HttpMethod.Get, T212Endpoint.ListReports, "", null, cancellationToken).ConfigureAwait(false);

    public async Task<Stream> DownloadReportAsync(Uri downloadLink, CancellationToken cancellationToken = default)
    {
        if (!downloadLink.IsAbsoluteUri || downloadLink.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("A report link must be an absolute https URL.", nameof(downloadLink));

        // Deliberately without the Authorization header: the link is pre-signed and points to another host.
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadLink);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new T212ApiException($"Downloading the report failed with {(int)response.StatusCode}.", response.StatusCode, downloadLink.Host);

        var copy = new MemoryStream();
        await response.Content.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        copy.Position = 0;
        return copy;
    }

    /// <summary>
    /// Follows <c>nextPagePath</c> until it is empty.
    /// </summary>
    /// <remarks>
    /// Only the query string of <c>nextPagePath</c> is used, on the endpoint's own path: users report
    /// paths in other formats (older routes, missing prefixes). A query already seen ends the walk with
    /// an error, so a cursor bug on Trading 212's side cannot loop forever.
    /// </remarks>
    private async IAsyncEnumerable<T212Page<T>> GetPagesAsync<T>(
        T212Endpoint endpoint,
        string firstQuery,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? query = firstQuery;

        while (query is not null)
        {
            if (!seen.Add(query))
                throw new T212ApiException($"Trading 212 returned a page cursor it had already returned ({endpoint.Path}{query}); stopped to avoid a loop.", endpoint: endpoint.Path);

            var page = await SendAsync<PaginatedResponse<T>>(HttpMethod.Get, endpoint, query, null, cancellationToken).ConfigureAwait(false);
            var next = NextQuery(page.NextPagePath);

            yield return new T212Page<T>(page.Items ?? [], next is not null);

            query = next;
        }
    }

    internal static string? NextQuery(string? nextPagePath)
    {
        if (string.IsNullOrWhiteSpace(nextPagePath))
            return null;

        var start = nextPagePath.IndexOf('?');
        return start < 0 || start == nextPagePath.Length - 1 ? null : nextPagePath[start..];
    }

    internal static string Query(params (string Name, string? Value)[] parameters)
    {
        var builder = new StringBuilder();
        foreach (var (name, value) in parameters)
        {
            if (string.IsNullOrEmpty(value))
                continue;

            builder.Append(builder.Length == 0 ? '?' : '&')
                .Append(name).Append('=').Append(Uri.EscapeDataString(value));
        }

        return builder.ToString();
    }

    private async Task<T> SendAsync<T>(HttpMethod method, T212Endpoint endpoint, string query, object? body, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetriesAsync(method, endpoint, query, body, cancellationToken).ConfigureAwait(false);

        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
            return value ?? throw new T212ApiException($"Trading 212 returned an empty body for {endpoint.Path}.", response.StatusCode, endpoint.Path);
        }
        catch (JsonException ex)
        {
            throw new T212ApiException($"Trading 212 returned a body that could not be read for {endpoint.Path}: {ex.Message}", response.StatusCode, endpoint.Path, ex);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetriesAsync(HttpMethod method, T212Endpoint endpoint, string query, object? body, CancellationToken cancellationToken)
    {
        var limitedCount = 0;
        var transientCount = 0;

        while (true)
        {
            await rateLimiter.WaitForTurnAsync(endpoint, cancellationToken).ConfigureAwait(false);

            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(method, new Uri(account.BaseAddress, endpoint.Path + query));
                request.Headers.TryAddWithoutValidation("Authorization", account.AuthorizationHeader);
                if (body is not null)
                    request.Content = JsonContent.Create(body, options: Json);

                response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                if (++transientCount > MaxTransientRetries)
                    throw new T212ApiException($"Trading 212 could not be reached for {endpoint.Path}: {ex.Message}", endpoint: endpoint.Path, inner: ex);

                logger.LogWarning("Trading 212 unreachable for {Endpoint} ({Error}); retry {Attempt} of {Max}", endpoint.Path, ex.Message, transientCount, MaxTransientRetries);
                await delayer.DelayAsync(Backoff(transientCount), cancellationToken).ConfigureAwait(false);
                continue;
            }

            rateLimiter.Observe(endpoint, response);

            if (response.IsSuccessStatusCode)
                return response;

            if (response.StatusCode == HttpStatusCode.TooManyRequests && ++limitedCount <= MaxRateLimitRetries)
            {
                var wait = rateLimiter.OnLimited(endpoint, response);
                logger.LogInformation("Rate limited on {Endpoint}; waiting {Wait} (retry {Attempt} of {Max})", endpoint.Path, wait, limitedCount, MaxRateLimitRetries);
                response.Dispose();
                continue;
            }

            if (IsTransient(response.StatusCode) && ++transientCount <= MaxTransientRetries)
            {
                logger.LogWarning("Trading 212 answered {Status} for {Endpoint}; retry {Attempt} of {Max}", (int)response.StatusCode, endpoint.Path, transientCount, MaxTransientRetries);
                response.Dispose();
                await delayer.DelayAsync(Backoff(transientCount), cancellationToken).ConfigureAwait(false);
                continue;
            }

            using (response)
                throw await ToExceptionAsync(response, endpoint, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or >= HttpStatusCode.InternalServerError;

    private static TimeSpan Backoff(int attempt) => TimeSpan.FromSeconds(2 * Math.Pow(2, attempt - 1));

    private async Task<T212ApiException> ToExceptionAsync(HttpResponseMessage response, T212Endpoint endpoint, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (content.Length > 500)
            content = content[..500];

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized when account.IsLegacyKey =>
                $"Trading 212 refused the API key of account '{account.Key}' (401). It has no secret: if it is an older key, Trading 212 may no longer accept it; generate a new key pair. Check too that the key was made for {account.Environment}, and that its allowed IP addresses include this server's.",
            HttpStatusCode.Unauthorized =>
                $"Trading 212 refused the API key of account '{account.Key}' (401). Check the key and the secret, that the key was made for {account.Environment}, and that its allowed IP addresses include this server's.",
            HttpStatusCode.Forbidden =>
                $"The API key of account '{account.Key}' lacks the permission for {endpoint.Path} (403). {content}".TrimEnd(),
            HttpStatusCode.TooManyRequests =>
                $"Still rate limited on {endpoint.Path} after {MaxRateLimitRetries} waits.",
            _ =>
                $"Trading 212 answered {(int)response.StatusCode} for {endpoint.Path}. {content}".TrimEnd(),
        };

        return new T212ApiException(message, response.StatusCode, endpoint.Path);
    }
}
