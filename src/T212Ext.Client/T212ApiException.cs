using System.Net;

namespace Arisoul.T212Ext.Client;

/// <summary>A call to the Trading 212 API that failed for good (after any retries).</summary>
public sealed class T212ApiException(string message, HttpStatusCode? statusCode = null, string? endpoint = null, Exception? inner = null)
    : Exception(message, inner)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string? Endpoint { get; } = endpoint;

    /// <summary>401: the key or the secret is wrong, or the key belongs to the other environment.</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;

    /// <summary>403: the key lacks the permission (scope) this endpoint needs.</summary>
    public bool IsMissingScope => StatusCode == HttpStatusCode.Forbidden;
}
