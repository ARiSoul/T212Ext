using System.Text;

namespace Arisoul.T212Ext.Client;

public enum T212Environment
{
    Live,
    Demo,
}

/// <summary>
/// One Trading 212 account (Invest or Stocks ISA) and the API key that reads it.
/// Each account needs its own key, and a key works only in the environment it was made for.
/// </summary>
/// <remarks>
/// Keys made since about October 2025 come in pairs (key and secret) and use HTTP Basic.
/// Older keys have no secret: they are sent as they are in the Authorization header, which
/// the API still accepts (<c>legacyApiKeyHeader</c> in the spec) with no announced end date.
/// </remarks>
public sealed record T212Account(string Key, T212Environment Environment, string ApiKey, string ApiSecret)
{
    public Uri BaseAddress => Environment switch
    {
        T212Environment.Live => new Uri("https://live.trading212.com"),
        T212Environment.Demo => new Uri("https://demo.trading212.com"),
        _ => throw new ArgumentOutOfRangeException(nameof(Environment), Environment, null),
    };

    public bool HasCredentials => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>A key made before key pairs existed: no secret.</summary>
    public bool IsLegacyKey => string.IsNullOrWhiteSpace(ApiSecret);

    /// <summary>
    /// The Authorization header value: HTTP Basic with the key as user name and the secret as
    /// password, or the bare key for a legacy key.
    /// </summary>
    public string AuthorizationHeader => IsLegacyKey
        ? ApiKey.Trim()
        : "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApiKey.Trim()}:{ApiSecret.Trim()}"));

    // Never print the key or the secret.
    public override string ToString() => $"{Key} ({Environment})";
}
