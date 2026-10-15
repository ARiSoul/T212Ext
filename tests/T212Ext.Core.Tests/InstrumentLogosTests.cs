using System.Net;
using System.Net.Http.Headers;
using Arisoul.T212Ext.Core.Logos;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Tests;

[Collection(DatabaseCollection.Name)]
public class InstrumentLogosTests(DatabaseFixture database)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeHttp _http = new();

    // Each test its own ISIN: the logos table is shared.
    private static string NewIsin() => "US" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private InstrumentLogos Logos(LogoOptions? options = null) =>
        new(database, _http, Options.Create(options ?? new LogoOptions()), _time, NullLogger<InstrumentLogos>.Instance);

    [Fact]
    public async Task Trading_212s_image_comes_first_and_is_kept()
    {
        var isin = NewIsin();
        _http.Answer("trading212equities", HttpStatusCode.OK, Png, "image/png");

        var logo = await Logos().GetAsync("AAPL_US_EQ", isin);
        var again = await Logos().GetAsync("AAPL_US_EQ", isin);

        Assert.Equal(Png, logo!.Data);
        Assert.Equal("image/png", again!.ContentType);
        Assert.Equal(["https://trading212equities.s3.eu-central-1.amazonaws.com/AAPL_US_EQ.png"], _http.Asked);   // once
    }

    [Fact]
    public async Task Parqet_is_asked_by_isin_when_trading_212_has_none()
    {
        var isin = NewIsin();
        _http.Answer("trading212equities", HttpStatusCode.Forbidden, [], "application/xml");
        _http.Answer("parqet", HttpStatusCode.OK, "<svg/>"u8.ToArray(), "image/svg+xml");

        var logo = await Logos().GetAsync("AAPL_US_EQ", isin);

        Assert.Equal("image/svg+xml", logo!.ContentType);
        Assert.Equal($"https://assets.parqet.com/logos/isin/{isin}", _http.Asked[^1]);
    }

    [Fact]
    public async Task Without_a_logo_none_is_asked_again_until_the_retry_time()
    {
        var isin = NewIsin();

        Assert.Null(await Logos().GetAsync("ZZZZ_US_EQ", isin));
        Assert.Null(await Logos().GetAsync("ZZZZ_US_EQ", isin));
        Assert.Equal(2, _http.Asked.Count);   // both sources, once

        _time.Now = _time.Now.AddDays(8);
        _http.Answer("parqet", HttpStatusCode.OK, Png, "image/png");
        Assert.NotNull(await Logos().GetAsync("ZZZZ_US_EQ", isin));
    }

    [Fact]
    public async Task Answers_that_are_not_images_are_refused()
    {
        _http.Answer("trading212equities", HttpStatusCode.OK, "<html/>"u8.ToArray(), "text/html");
        _http.Answer("parqet", HttpStatusCode.OK, new byte[InstrumentLogos.MaxBytes + 1], "image/png");

        Assert.Null(await Logos().GetAsync("AAPL_US_EQ", NewIsin()));
    }

    [Fact]
    public async Task Turned_off_nothing_is_fetched()
    {
        Assert.Null(await Logos(new LogoOptions { Enabled = false }).GetAsync("AAPL_US_EQ", NewIsin()));
        Assert.Empty(_http.Asked);
    }

    [Theory]
    [InlineData("AAPL_US_EQ", "US0378331005", 2)]
    [InlineData("AAPL", "US0378331005", 1)]           // a CSV row's plain symbol: only by ISIN
    [InlineData("AAPL_US_EQ", null, 1)]
    [InlineData("../etc/passwd_x", "bad", 0)]        // nothing but tickers and ISINs reaches a URL
    [InlineData("A/B_US_EQ", "US03783310", 0)]
    public void Sources_are_fixed_hosts_with_checked_keys(string ticker, string? isin, int count) =>
        Assert.Equal(count, InstrumentLogos.Sources(ticker, isin).Count);

    private sealed class FakeHttp : IHttpClientFactory
    {
        private readonly Dictionary<string, (HttpStatusCode Status, byte[] Body, string Type)> _answers = [];

        public List<string> Asked { get; } = [];

        public void Answer(string hostPart, HttpStatusCode status, byte[] body, string type) => _answers[hostPart] = (status, body, type);

        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler(FakeHttp owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                owner.Asked.Add(url);
                var (status, body, type) = owner._answers.FirstOrDefault(a => url.Contains(a.Key)).Value;
                var response = new HttpResponseMessage(status == 0 ? HttpStatusCode.NotFound : status) { Content = new ByteArrayContent(body ?? []) };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(type ?? "application/octet-stream");
                return Task.FromResult(response);
            }
        }
    }
}
