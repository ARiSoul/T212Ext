using System.Net;
using System.Text;
using Arisoul.T212Ext.Client.Models;
using Arisoul.T212Ext.Client.Tests.Infrastructure;

namespace Arisoul.T212Ext.Client.Tests;

public class T212ApiClientTests
{
    [Fact]
    public async Task Requests_use_basic_auth_with_key_and_secret_on_the_live_host()
    {
        var harness = new ClientHarness();
        harness.Handler.Respond(HttpStatusCode.OK, "{}");

        await harness.Client.GetAccountSummaryAsync();

        var request = Assert.Single(harness.Handler.Requests);
        Assert.Equal("https://live.trading212.com/api/v0/equity/account/summary", request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal("key-123:secret-456", Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter!)));
    }

    [Fact]
    public async Task An_older_key_without_a_secret_is_sent_as_it_is()
    {
        var harness = new ClientHarness(secret: "");
        harness.Handler.Respond(HttpStatusCode.OK, "{}");

        await harness.Client.GetAccountSummaryAsync();

        Assert.True(harness.Account.HasCredentials);
        Assert.True(harness.Account.IsLegacyKey);
        Assert.Equal("key-123", Assert.Single(harness.Handler.Requests[0].Headers.GetValues("Authorization")));
    }

    [Fact]
    public async Task A_refused_older_key_suggests_a_new_key_pair()
    {
        var harness = new ClientHarness(secret: "");
        harness.Handler.Respond(HttpStatusCode.Unauthorized, "");

        var error = await Assert.ThrowsAsync<T212ApiException>(() => harness.Client.GetAccountSummaryAsync());

        Assert.Contains("new key pair", error.Message);
    }

    [Fact]
    public void An_account_without_a_key_has_no_credentials() =>
        Assert.False(new T212Account("invest", T212Environment.Live, " ", "secret").HasCredentials);

    [Fact]
    public async Task Demo_accounts_call_the_demo_host()
    {
        var harness = new ClientHarness(T212Environment.Demo);
        harness.Handler.Respond(HttpStatusCode.OK, "[]");

        await harness.Client.GetPositionsAsync();

        Assert.Equal("demo.trading212.com", harness.Handler.Requests[0].RequestUri!.Host);
    }

    [Fact]
    public void The_account_never_prints_its_secret()
    {
        var account = new T212Account("invest", T212Environment.Live, "key-123", "secret-456");

        Assert.DoesNotContain("secret-456", account.ToString());
        Assert.DoesNotContain("key-123", account.ToString());
    }

    [Fact]
    public async Task Account_summary_is_read()
    {
        var harness = new ClientHarness();
        harness.Handler.Respond(HttpStatusCode.OK, """
            {"id":123456,"currency":"EUR","totalValue":10500.5,
             "cash":{"availableToTrade":250.25,"inPies":0,"reservedForOrders":10},
             "investments":{"currentValue":10250.25,"totalCost":9000,"realizedProfitLoss":120.5,"unrealizedProfitLoss":1250.25}}
            """);

        var summary = await harness.Client.GetAccountSummaryAsync();

        Assert.Equal(123456, summary.Id);
        Assert.Equal("EUR", summary.Currency);
        Assert.Equal(10500.5m, summary.TotalValue);
        Assert.Equal(250.25m, summary.Cash!.AvailableToTrade);
        Assert.Equal(1250.25m, summary.Investments!.UnrealizedProfitLoss);
    }

    [Fact]
    public async Task Historical_orders_keep_the_order_and_its_fill()
    {
        var harness = new ClientHarness();
        harness.Handler.Respond(HttpStatusCode.OK, """
            {"items":[{
              "order":{"id":42,"ticker":"AAPL_US_EQ","side":"BUY","type":"MARKET","strategy":"VALUE","status":"FILLED",
                       "initiatedFrom":"AUTOINVEST","timeInForce":"DAY","currency":"EUR","createdAt":"2026-01-05T14:30:00Z",
                       "value":100,"filledValue":100,"filledQuantity":0.5123456789,
                       "instrument":{"ticker":"AAPL_US_EQ","isin":"US0378331005","name":"Apple","currency":"USD"},
                       "someFieldAddedLater":"ignored"},
              "fill":{"id":7,"filledAt":"2026-01-05T14:30:02Z","price":228.11,"quantity":0.5123456789,"type":"TRADE","tradingMethod":"TOTV",
                      "walletImpact":{"currency":"EUR","fxRate":1.0842,"netValue":-100,"realisedProfitLoss":0,
                                      "taxes":[{"name":"CURRENCY_CONVERSION_FEE","quantity":0.15,"currency":"EUR","chargedAt":"2026-01-05T14:30:02Z"}]}}
            }],"nextPagePath":null}
            """);

        var pages = await ToListAsync(harness.Client.GetOrderPagesAsync());

        var page = Assert.Single(pages);
        Assert.False(page.HasMore);
        var item = Assert.Single(page.Items);
        Assert.Equal(42, item.Order!.Id);
        Assert.Equal("US0378331005", item.Order.Instrument!.Isin);
        Assert.Equal(0.5123456789m, item.Fill!.Quantity);
        Assert.Equal(1.0842m, item.Fill.WalletImpact!.FxRate);
        Assert.Equal("CURRENCY_CONVERSION_FEE", Assert.Single(item.Fill.WalletImpact.Taxes!).Name);
    }

    [Fact]
    public async Task Dividend_pages_follow_the_cursor_on_the_endpoint_own_path()
    {
        var harness = new ClientHarness();
        // The next page path comes in an older route format; only its query string is used.
        harness.Handler
            .Respond(HttpStatusCode.OK, """{"items":[{"reference":"a","paidOn":"2026-03-01T00:00:00Z","type":"ORDINARY"}],"nextPagePath":"/api/v0/history/dividends?limit=50&cursor=1700000000000"}""")
            .Respond(HttpStatusCode.OK, """{"items":[{"reference":"b","paidOn":"2026-02-01T00:00:00Z","type":"BRAND_NEW_TYPE"}],"nextPagePath":null}""");

        var pages = await ToListAsync(harness.Client.GetDividendPagesAsync());

        Assert.Equal(["a", "b"], pages.SelectMany(p => p.Items).Select(i => i.Reference));
        Assert.True(pages[0].HasMore);
        Assert.False(pages[1].HasMore);
        Assert.Equal("/api/v0/equity/history/dividends?limit=50", harness.Handler.Requests[0].RequestUri!.PathAndQuery);
        Assert.Equal("/api/v0/equity/history/dividends?limit=50&cursor=1700000000000", harness.Handler.Requests[1].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task A_cursor_seen_twice_stops_the_walk_instead_of_looping()
    {
        var harness = new ClientHarness();
        const string page = """{"items":[],"nextPagePath":"/api/v0/equity/history/orders?limit=50&cursor=5"}""";
        harness.Handler.Respond(HttpStatusCode.OK, page).Respond(HttpStatusCode.OK, page);

        var error = await Assert.ThrowsAsync<T212ApiException>(() => ToListAsync(harness.Client.GetOrderPagesAsync()));

        Assert.Contains("already returned", error.Message);
        Assert.Equal(2, harness.Handler.Requests.Count);
    }

    [Fact]
    public async Task Ticker_filter_is_escaped_into_the_query()
    {
        var harness = new ClientHarness();
        harness.Handler.Respond(HttpStatusCode.OK, """{"items":[]}""");

        await ToListAsync(harness.Client.GetDividendPagesAsync("BRK.B_US_EQ"));

        Assert.Equal("?limit=50&ticker=BRK.B_US_EQ", harness.Handler.Requests[0].RequestUri!.Query);
    }

    [Fact]
    public async Task A_429_waits_until_the_reset_header_then_retries()
    {
        var harness = new ClientHarness();
        harness.Handler
            .Respond(HttpStatusCode.TooManyRequests, "", ClientHarness.RateHeaders(0, ClientHarness.Start.AddSeconds(30)))
            .Respond(HttpStatusCode.OK, "[]");

        await harness.Client.GetPositionsAsync();

        Assert.Equal(2, harness.Handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(30)], harness.Delayer.Delays);
    }

    [Fact]
    public async Task A_429_without_headers_waits_the_documented_period()
    {
        var harness = new ClientHarness();
        harness.Handler
            .Respond(HttpStatusCode.TooManyRequests, "")
            .Respond(HttpStatusCode.OK, """{"items":[]}""");

        await ToListAsync(harness.Client.GetDividendPagesAsync());

        Assert.Equal([TimeSpan.FromMinutes(1)], harness.Delayer.Delays);
    }

    [Fact]
    public async Task With_nothing_remaining_the_next_call_waits_for_the_reset_first()
    {
        var harness = new ClientHarness();
        harness.Handler
            .Respond(HttpStatusCode.OK, "{}", ClientHarness.RateHeaders(0, ClientHarness.Start.AddSeconds(5)))
            .Respond(HttpStatusCode.OK, "{}");

        await harness.Client.GetAccountSummaryAsync();
        Assert.Empty(harness.Delayer.Delays);

        await harness.Client.GetAccountSummaryAsync();
        Assert.Equal([TimeSpan.FromSeconds(5)], harness.Delayer.Delays);
    }

    [Fact]
    public async Task Rate_limits_are_kept_per_endpoint()
    {
        var harness = new ClientHarness();
        harness.Handler
            .Respond(HttpStatusCode.OK, "{}", ClientHarness.RateHeaders(0, ClientHarness.Start.AddSeconds(5)))
            .Respond(HttpStatusCode.OK, "[]");

        await harness.Client.GetAccountSummaryAsync();
        await harness.Client.GetPositionsAsync();

        Assert.Empty(harness.Delayer.Delays);
    }

    [Fact]
    public async Task Still_limited_after_every_retry_gives_up()
    {
        var harness = new ClientHarness();
        for (var i = 0; i <= T212ApiClient.MaxRateLimitRetries; i++)
            harness.Handler.Respond(HttpStatusCode.TooManyRequests, "");

        var error = await Assert.ThrowsAsync<T212ApiException>(() => harness.Client.GetPositionsAsync());

        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.Equal(T212ApiClient.MaxRateLimitRetries + 1, harness.Handler.Requests.Count);
    }

    [Fact]
    public async Task A_refused_key_fails_at_once_with_a_clear_message()
    {
        var harness = new ClientHarness();
        harness.Handler.Respond(HttpStatusCode.Unauthorized, "");

        var error = await Assert.ThrowsAsync<T212ApiException>(() => harness.Client.GetAccountSummaryAsync());

        Assert.True(error.IsUnauthorized);
        Assert.Contains("'invest'", error.Message);
        Assert.Contains("IP addresses", error.Message);   // a key restricted to another address also answers 401
        Assert.DoesNotContain("secret-456", error.Message);
        Assert.Single(harness.Handler.Requests);
    }

    [Fact]
    public async Task A_missing_scope_names_the_endpoint()
    {
        var harness = new ClientHarness();
        harness.Handler.Respond(HttpStatusCode.Forbidden, "Scope( history:dividends ) missing for API key");

        var error = await Assert.ThrowsAsync<T212ApiException>(() => ToListAsync(harness.Client.GetDividendPagesAsync()));

        Assert.True(error.IsMissingScope);
        Assert.Contains("/history/dividends", error.Message);
        Assert.Contains("history:dividends", error.Message);
    }

    [Fact]
    public async Task Server_errors_and_network_failures_are_retried_with_backoff()
    {
        var harness = new ClientHarness();
        harness.Handler
            .Respond(HttpStatusCode.ServiceUnavailable, "")
            .Throw(new HttpRequestException("connection reset"))
            .Respond(HttpStatusCode.OK, "[]");

        await harness.Client.GetPositionsAsync();

        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], harness.Delayer.Delays);
    }

    [Fact]
    public async Task An_unreadable_body_is_reported_as_an_api_error()
    {
        var harness = new ClientHarness();
        harness.Handler.Respond(HttpStatusCode.OK, "<html>maintenance</html>");

        var error = await Assert.ThrowsAsync<T212ApiException>(() => harness.Client.GetAccountSummaryAsync());

        Assert.Contains("could not be read", error.Message);
    }

    [Fact]
    public async Task Reports_are_downloaded_without_credentials()
    {
        var harness = new ClientHarness();
        harness.Handler.Respond(HttpStatusCode.OK, "Action,Time\n");

        await using var stream = await harness.Client.DownloadReportAsync(new Uri("https://reports.example.com/report.csv?signature=abc"));

        Assert.Null(harness.Handler.Requests[0].Headers.Authorization);
        Assert.Equal("Action,Time\n", await new StreamReader(stream).ReadToEndAsync());
    }

    [Fact]
    public async Task Report_requests_send_the_period_and_return_the_id()
    {
        var harness = new ClientHarness();
        harness.Handler.Respond(HttpStatusCode.OK, """{"reportId":99}""");

        var id = await harness.Client.RequestReportAsync(new ReportRequest
        {
            TimeFrom = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TimeTo = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        });

        Assert.Equal(99, id);
        var body = harness.Handler.Bodies[0]!;
        Assert.Contains("\"timeFrom\":\"2025-01-01T00:00:00+00:00\"", body);
        Assert.Contains("\"includeDividends\":true", body);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("/api/v0/equity/history/dividends", null)]
    [InlineData("/api/v0/equity/history/dividends?", null)]
    [InlineData("/api/v0/equity/history/dividends?limit=50&cursor=9", "?limit=50&cursor=9")]
    [InlineData("history/dividends?cursor=9", "?cursor=9")]
    public void Next_query_is_the_query_string_of_the_next_page_path(string? nextPagePath, string? expected) =>
        Assert.Equal(expected, T212ApiClient.NextQuery(nextPagePath));

    private static async Task<List<T212Page<T>>> ToListAsync<T>(IAsyncEnumerable<T212Page<T>> pages)
    {
        var list = new List<T212Page<T>>();
        await foreach (var page in pages)
            list.Add(page);
        return list;
    }
}
