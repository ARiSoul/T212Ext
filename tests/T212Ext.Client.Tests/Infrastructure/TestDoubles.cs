using System.Net;
using System.Text;
using Arisoul.T212Ext.Client;
using Arisoul.T212Ext.Client.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arisoul.T212Ext.Client.Tests.Infrastructure;

/// <summary>A clock that moves only when told to.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Records each wait and moves the clock forward instead of sleeping.</summary>
public sealed class RecordingDelayer(ManualTimeProvider time) : IDelayer
{
    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        Delays.Add(delay);
        time.Now += delay;
        return Task.CompletedTask;
    }
}

/// <summary>Answers requests from a queue and keeps what was sent.</summary>
public sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Request bodies, read when sent (the client disposes the request afterwards).</summary>
    public List<string?> Bodies { get; } = [];

    public StubHandler Respond(HttpStatusCode status, string body = "{}", Action<HttpResponseMessage>? configure = null)
    {
        _responses.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            configure?.Invoke(response);
            return response;
        });
        return this;
    }

    public StubHandler Throw(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        if (_responses.Count == 0)
            throw new InvalidOperationException($"No response queued for {request.RequestUri}.");
        return _responses.Dequeue()(request);
    }
}

/// <summary>A client wired to a <see cref="StubHandler"/>, a manual clock and a recording delayer.</summary>
public sealed class ClientHarness
{
    public static readonly DateTimeOffset Start = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    public ClientHarness(T212Environment environment = T212Environment.Live, string secret = "secret-456")
    {
        Time = new ManualTimeProvider(Start);
        Delayer = new RecordingDelayer(Time);
        Account = new T212Account("invest", environment, "key-123", secret);
        Client = new T212ApiClient(new HttpClient(Handler), Account, new T212RateLimiter(Time, Delayer), Delayer, NullLogger<T212ApiClient>.Instance);
    }

    public StubHandler Handler { get; } = new();
    public ManualTimeProvider Time { get; }
    public RecordingDelayer Delayer { get; }
    public T212Account Account { get; }
    public T212ApiClient Client { get; }

    public static Action<HttpResponseMessage> RateHeaders(int remaining, DateTimeOffset reset) => response =>
    {
        response.Headers.Add("x-ratelimit-remaining", remaining.ToString());
        response.Headers.Add("x-ratelimit-reset", reset.ToUnixTimeSeconds().ToString());
    };
}
