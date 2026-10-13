using System.Collections.Concurrent;
using Arisoul.T212Ext.Client.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Arisoul.T212Ext.Client;

public interface IT212ApiFactory
{
    IT212Api Create(T212Account account);
}

/// <summary>Creates API clients; keeps one rate limiter per account, since Trading 212 limits per account.</summary>
public sealed class T212ApiFactory(
    IHttpClientFactory httpClientFactory,
    TimeProvider time,
    IDelayer delayer,
    ILoggerFactory loggerFactory) : IT212ApiFactory
{
    public const string HttpClientName = "T212";

    private readonly ConcurrentDictionary<string, T212RateLimiter> _limiters = new(StringComparer.Ordinal);

    public IT212Api Create(T212Account account)
    {
        if (!account.HasCredentials)
            throw new InvalidOperationException($"Account '{account.Key}' has no API key.");

        var limiter = _limiters.GetOrAdd($"{account.Environment}:{account.Key}", _ => new T212RateLimiter(time, delayer));
        return new T212ApiClient(
            httpClientFactory.CreateClient(HttpClientName),
            account,
            limiter,
            delayer,
            loggerFactory.CreateLogger<T212ApiClient>());
    }
}

public static class T212ClientServiceCollectionExtensions
{
    public static IServiceCollection AddT212Client(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDelayer, TimeProviderDelayer>();
        services.AddHttpClient(T212ApiFactory.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(100));
        services.TryAddSingleton<IT212ApiFactory, T212ApiFactory>();
        return services;
    }
}
