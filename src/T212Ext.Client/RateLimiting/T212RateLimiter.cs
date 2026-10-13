using System.Globalization;
using System.Net.Http.Headers;

namespace Arisoul.T212Ext.Client.RateLimiting;

/// <summary>Waits for a span of time. Exists so tests can record waits instead of sleeping.</summary>
public interface IDelayer
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

internal sealed class TimeProviderDelayer(TimeProvider time) : IDelayer
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, time, cancellationToken);
}

/// <summary>
/// The rate limit state of one Trading 212 account, per endpoint.
/// </summary>
/// <remarks>
/// Trading 212 limits per account (not per key or IP) and allows bursts. Every response carries
/// <c>x-ratelimit-remaining</c> and <c>x-ratelimit-reset</c> (Unix time); when nothing remains,
/// the next call waits until the reset. Some 429s come without those headers (reported for
/// <c>/history/exports</c>), so each endpoint also has the period from the docs as a fallback.
/// </remarks>
public sealed class T212RateLimiter(TimeProvider time, IDelayer delayer)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, State> _states = new(StringComparer.Ordinal);

    public async Task WaitForTurnAsync(T212Endpoint endpoint, CancellationToken cancellationToken)
    {
        var wait = TimeSpan.Zero;
        lock (_lock)
        {
            if (_states.TryGetValue(endpoint.Key, out var state) && state.Remaining <= 0 && state.ResetAt is { } resetAt)
            {
                wait = resetAt - time.GetUtcNow();
                if (wait <= TimeSpan.Zero)
                    _states.Remove(endpoint.Key);
            }
        }

        if (wait > TimeSpan.Zero)
            await delayer.DelayAsync(wait, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Records the rate limit headers of any response.</summary>
    public void Observe(T212Endpoint endpoint, HttpResponseMessage response)
    {
        var remaining = ReadInt(response.Headers, "x-ratelimit-remaining");
        var resetAt = ReadUnixTime(response.Headers, "x-ratelimit-reset");
        if (remaining is null && resetAt is null)
            return;

        lock (_lock)
            _states[endpoint.Key] = new State(remaining, resetAt);
    }

    /// <summary>Records a 429. The next <see cref="WaitForTurnAsync"/> waits until the reset; returns how long that is.</summary>
    public TimeSpan OnLimited(T212Endpoint endpoint, HttpResponseMessage response)
    {
        var now = time.GetUtcNow();
        var resetAt = ReadUnixTime(response.Headers, "x-ratelimit-reset");
        if (resetAt is null || resetAt <= now)
            resetAt = now + endpoint.Period;

        lock (_lock)
            _states[endpoint.Key] = new State(0, resetAt);

        return resetAt.Value - now;
    }

    private static int? ReadInt(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values)
        && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static DateTimeOffset? ReadUnixTime(HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values)
            || !long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return null;

        // The docs say "Unix timestamp" without the unit; accept milliseconds too.
        return value > 100_000_000_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : DateTimeOffset.FromUnixTimeSeconds(value);
    }

    private readonly record struct State(int? Remaining, DateTimeOffset? ResetAt);
}
