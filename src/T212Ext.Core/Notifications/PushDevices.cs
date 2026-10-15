using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Notifications;

/// <summary>A browser's Web Push subscription, as the page reads it from <c>PushManager</c>.</summary>
public sealed record PushSubscriptionInfo(string Endpoint, string P256dh, string Auth);

public sealed record PushDeviceRow(long Id, string Username, string Label, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, string EndpointHash, PushPreferences Preferences);

/// <summary>
/// The devices with notifications on, and sending to them. A subscription's address must be a known
/// push service over HTTPS: the server posts to it, so any other address (the server's own network,
/// for one) is refused.
/// </summary>
public sealed class PushDevices(
    IDbContextFactory<T212ExtDbContext> dbFactory,
    IPushSender sender,
    IPushTexts texts,
    IOptions<PushOptions> options,
    TimeProvider time,
    ILogger<PushDevices> logger)
{
    public const int MaxDevices = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // The browsers' push services: Google (Chrome, Edge on Android), Mozilla, Microsoft (Edge, Windows)
    // and Apple (Safari, iPhone). Any host under them.
    private static readonly string[] PushDomains = ["googleapis.com", "google.com", "mozilla.com", "windows.com", "apple.com"];

    public bool IsConfigured => options.Value.IsConfigured;

    /// <summary>The key the browser needs to subscribe; null when notifications are off.</summary>
    public string? PublicKey => IsConfigured ? options.Value.PublicKey : null;

    public static bool IsPushServiceUrl(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && PushDomains.Any(d => uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));

    public static string Hash(string endpoint) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));

    public async Task<IReadOnlyList<PushDeviceRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PushDevices.AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new PushDeviceRow(d.Id, d.Username, d.Label, d.CreatedAt, d.LastUsedAt, d.EndpointHash,
                new PushPreferences(d.NotifyDividends, d.NotifySummary, d.NotifyWeek, d.QuietFrom, d.QuietTo, d.TimeZone)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>A device's choices; null when it is not registered.</summary>
    public async Task<PushPreferences?> GetPreferencesAsync(string endpointHash, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PushDevices.AsNoTracking()
            .Where(d => d.EndpointHash == endpointHash)
            .Select(d => new PushPreferences(d.NotifyDividends, d.NotifySummary, d.NotifyWeek, d.QuietFrom, d.QuietTo, d.TimeZone))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>Saves a device's choices; false when it is not registered. Quiet hours need both ends, and different ones (else none).</summary>
    public async Task<bool> SetPreferencesAsync(string endpointHash, PushPreferences preferences, CancellationToken cancellationToken = default)
    {
        var quiet = preferences.QuietFrom is { } f && preferences.QuietTo is { } t && f != t;
        var from = quiet ? preferences.QuietFrom : null;
        var to = quiet ? preferences.QuietTo : null;
        var zone = TimeZoneOrNull(preferences.TimeZone);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var device = await db.PushDevices.SingleOrDefaultAsync(d => d.EndpointHash == endpointHash, cancellationToken);
        if (device is null)
            return false;

        device.NotifyDividends = preferences.Dividends;
        device.NotifySummary = preferences.Summary;
        device.NotifyWeek = preferences.Week;
        device.QuietFrom = from;
        device.QuietTo = to;
        device.TimeZone = zone ?? device.TimeZone;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Whether <paramref name="now"/> falls in the device's quiet hours, in its time zone (UTC when unknown).</summary>
    public static bool IsQuiet(PushPreferences preferences, DateTimeOffset now)
    {
        if (preferences.QuietFrom is not { } from || preferences.QuietTo is not { } to || from == to)
            return false;

        var zone = TimeZoneOrNull(preferences.TimeZone) is { } id ? TimeZoneInfo.FindSystemTimeZoneById(id) : TimeZoneInfo.Utc;
        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return from < to ? local >= from && local < to : local >= from || local < to;
    }

    /// <summary>An IANA time zone the server knows ("Europe/Lisbon"), or null.</summary>
    public static string? TimeZoneOrNull(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
            return null;
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return id;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    /// <summary>Saves a subscription (or renews the same browser's); false when it is refused.</summary>
    public async Task<bool> RegisterAsync(string username, PushSubscriptionInfo subscription, string label, CultureInfo culture, string? timeZone = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || !IsPushServiceUrl(subscription.Endpoint) || subscription.Endpoint.Length > 2048
            || string.IsNullOrWhiteSpace(subscription.P256dh) || subscription.P256dh.Length > 200
            || string.IsNullOrWhiteSpace(subscription.Auth) || subscription.Auth.Length > 100)
        {
            // The host only: the rest of the address is the device's secret.
            logger.LogWarning("Push subscription refused: host {Host}",
                Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var refused) ? refused.Host : "(not a URL)");
            return false;
        }

        label = string.IsNullOrWhiteSpace(label) ? "Browser" : label.Trim()[..Math.Min(label.Trim().Length, 100)];
        var hash = Hash(subscription.Endpoint);
        var zone = TimeZoneOrNull(timeZone);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var device = await db.PushDevices.SingleOrDefaultAsync(d => d.EndpointHash == hash, cancellationToken);
        if (device is null)
        {
            if (await db.PushDevices.CountAsync(cancellationToken) >= MaxDevices)
                return false;

            db.PushDevices.Add(new PushDevice
            {
                Username = username, Endpoint = subscription.Endpoint, EndpointHash = hash, P256dh = subscription.P256dh,
                Auth = subscription.Auth, Label = label, Culture = culture.Name, CreatedAt = time.GetUtcNow(), TimeZone = zone,
            });
        }
        else
        {
            device.Username = username;
            device.P256dh = subscription.P256dh;
            device.Auth = subscription.Auth;
            device.Label = label;
            device.Culture = culture.Name;
            device.TimeZone = zone ?? device.TimeZone;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RemoveAsync(string endpointHash, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.PushDevices.Where(d => d.EndpointHash == endpointHash).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Sends a test notification to one device; true when the push service took it.</summary>
    public async Task<bool> SendTestAsync(string endpointHash, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var device = await db.PushDevices.SingleOrDefaultAsync(d => d.EndpointHash == endpointHash, cancellationToken);
        if (device is null || !IsConfigured)
            return false;

        var text = texts.Test(CultureOf(device));
        return await SendAsync([device], _ => new PushMessage(text.Title, text.Body, "/notifications", "test"), cancellationToken) > 0;
    }

    /// <summary>
    /// Sends a message to every device that chose its topic, each in its own language, silent in its
    /// quiet hours; removes the devices the push service no longer knows. Returns how many took it.
    /// </summary>
    public async Task<int> SendToAllAsync(PushTopic topic, Func<CultureInfo, PushMessage> message, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var devices = await db.PushDevices
            .Where(d => topic == PushTopic.Dividends ? d.NotifyDividends : topic == PushTopic.Summary ? d.NotifySummary : d.NotifyWeek)
            .ToListAsync(cancellationToken);
        var now = time.GetUtcNow();
        return await SendAsync(devices, (device, culture) => message(culture) with { Silent = IsQuiet(Preferences(device), now) }, cancellationToken);
    }

    private static PushPreferences Preferences(PushDevice d) => new(d.NotifyDividends, d.NotifySummary, d.NotifyWeek, d.QuietFrom, d.QuietTo, d.TimeZone);

    private Task<int> SendAsync(IReadOnlyList<PushDevice> devices, Func<CultureInfo, PushMessage> message, CancellationToken cancellationToken) =>
        SendAsync(devices, (_, culture) => message(culture), cancellationToken);

    private async Task<int> SendAsync(IReadOnlyList<PushDevice> devices, Func<PushDevice, CultureInfo, PushMessage> message, CancellationToken cancellationToken)
    {
        var delivered = 0;
        foreach (var device in devices)
        {
            var payload = JsonSerializer.Serialize(message(device, CultureOf(device)), Json);
            var result = await sender.SendAsync(device, payload, cancellationToken);

            await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
            if (result == PushResult.Gone)
            {
                logger.LogInformation("Push device {DeviceId} ({Label}) is gone; removed", device.Id, device.Label);
                await db.PushDevices.Where(d => d.Id == device.Id).ExecuteDeleteAsync(CancellationToken.None);
            }
            else if (result == PushResult.Delivered)
            {
                delivered++;
                var now = time.GetUtcNow();
                await db.PushDevices.Where(d => d.Id == device.Id).ExecuteUpdateAsync(s => s.SetProperty(d => d.LastUsedAt, now), CancellationToken.None);
            }
        }
        return delivered;
    }

    private static CultureInfo CultureOf(PushDevice device)
    {
        try
        {
            return CultureInfo.GetCultureInfo(device.Culture);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
