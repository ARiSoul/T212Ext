using System.Globalization;
using System.Net;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;

namespace Arisoul.T212Ext.Core.Notifications;

/// <summary>The VAPID keys ("Push" settings, <c>Push__*</c> in .env). Without them notifications are off.</summary>
public sealed class PushOptions
{
    public const string Section = "Push";

    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";

    /// <summary>A contact for the push services: <c>mailto:</c> or <c>https:</c>.</summary>
    public string Subject { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey) && !string.IsNullOrWhiteSpace(Subject);

    /// <summary>A phone that stays off longer than this does not get the message any more.</summary>
    public static readonly TimeSpan TimeToLive = TimeSpan.FromDays(1);

    /// <summary>A new pair of VAPID keys (the <c>new-vapid-keys</c> command).</summary>
    public static (string PublicKey, string PrivateKey) NewKeys()
    {
        var keys = VapidHelper.GenerateVapidKeys();
        return (keys.PublicKey, keys.PrivateKey);
    }
}

/// <summary>A notification: its text, the page a tap opens, and a tag that replaces an earlier one with the same tag.</summary>
public sealed record PushMessage(string Title, string Body, string Url, string Tag);

/// <summary>A notification's texts, in one language.</summary>
public sealed record PushText(string Title, string Body);

/// <summary>A dividend to announce.</summary>
public sealed record DividendNotice(string Name, decimal Amount, string Currency, DateTimeOffset PaidOn);

/// <summary>A month's dividends, per currency, and the same month a year before.</summary>
public sealed record MonthNotice(DateOnly Month, IReadOnlyList<(string Currency, decimal Amount, decimal? YearBefore)> Totals, int Count);

/// <summary>Writes the notifications' texts in a language. The web project implements it with its translations.</summary>
public interface IPushTexts
{
    PushText NewDividends(CultureInfo culture, IReadOnlyList<DividendNotice> dividends);
    PushText MonthlySummary(CultureInfo culture, MonthNotice month);
    PushText Test(CultureInfo culture);
}

public enum PushResult
{
    Delivered,

    /// <summary>The push service says the subscription no longer exists: the device is removed.</summary>
    Gone,

    Failed,
}

/// <summary>Sends one encrypted Web Push message to one device. Faked in tests.</summary>
public interface IPushSender
{
    Task<PushResult> SendAsync(PushDevice device, string payload, CancellationToken cancellationToken);
}

internal sealed class WebPushSender(IHttpClientFactory httpFactory, IOptions<PushOptions> options, ILogger<WebPushSender> logger) : IPushSender
{
    public const string HttpClientName = "WebPush";

    public async Task<PushResult> SendAsync(PushDevice device, string payload, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var sendOptions = new Dictionary<string, object>
        {
            ["vapidDetails"] = new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey),
            ["TTL"] = (int)PushOptions.TimeToLive.TotalSeconds,
            // Without it, Android holds the message while the phone sleeps and it arrives late.
            ["headers"] = new Dictionary<string, object> { ["Urgency"] = "high" },
        };

        try
        {
            var client = new WebPushClient(httpFactory.CreateClient(HttpClientName));
            await client.SendNotificationAsync(new PushSubscription(device.Endpoint, device.P256dh, device.Auth), payload, sendOptions, cancellationToken);
            return PushResult.Delivered;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushResult.Gone;
        }
        catch (Exception ex) when (ex is WebPushException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Push to device {DeviceId} failed", device.Id);
            return PushResult.Failed;
        }
    }
}
