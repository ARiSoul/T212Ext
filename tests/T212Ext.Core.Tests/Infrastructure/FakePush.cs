using System.Globalization;
using System.Text.Json;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Tests.Infrastructure;

/// <summary>Records what would be pushed; a device's endpoint in <see cref="Gone"/> answers Gone.</summary>
public sealed class FakePushSender : IPushSender
{
    public List<(string Endpoint, JsonElement Message)> Sent { get; } = [];
    public HashSet<string> Gone { get; } = [];

    public Task<PushResult> SendAsync(PushDevice device, string payload, CancellationToken cancellationToken)
    {
        if (Gone.Contains(device.Endpoint))
            return Task.FromResult(PushResult.Gone);

        Sent.Add((device.Endpoint, JsonDocument.Parse(payload).RootElement.Clone()));
        return Task.FromResult(PushResult.Delivered);
    }
}

/// <summary>Plain texts that show what they were given, and in which language.</summary>
public sealed class FakePushTexts : IPushTexts
{
    public PushText NewDividends(CultureInfo culture, IReadOnlyList<DividendNotice> dividends) =>
        new($"{culture.Name} dividends {dividends.Count}", string.Join(", ", dividends.Select(d => $"{d.Name} {N(d.Amount)} {d.Currency}")));

    public PushText MonthlySummary(CultureInfo culture, MonthNotice month) =>
        new($"{culture.Name} summary {month.Month:yyyy-MM}",
            string.Join(", ", month.Totals.Select(t => $"{N(t.Amount)} {t.Currency} (before {(t.YearBefore is { } b ? N(b) : "none")})")) + $" in {month.Count}");

    public PushText WeekAhead(CultureInfo culture, WeekNotice week) =>
        new($"{culture.Name} week {week.From:yyyy-MM-dd}",
            string.Join(", ", week.Payments.Select(p => $"{p.Name} {N(p.Amount)} {p.Currency} {p.On:MM-dd}")));

    public PushText Test(CultureInfo culture) => new($"{culture.Name} test", "");

    // Amounts come back from SQL Server with ten decimals.
    private static string N(decimal value) => value.ToString("0.##########", CultureInfo.InvariantCulture);
}

public static class Push
{
    public static PushOptions Keys => new() { PublicKey = "public", PrivateKey = "private", Subject = "mailto:test@example.com" };

    public static PushDevices Devices(DatabaseFixture database, FakePushSender sender, TimeProvider time, PushOptions? options = null) =>
        new(database, sender, new FakePushTexts(), Options.Create(options ?? Keys), time, NullLogger<PushDevices>.Instance);

    public static PushSubscriptionInfo Subscription(string endpoint = "https://fcm.googleapis.com/fcm/send/abc") => new(endpoint, "p256dh-key", "auth-secret");
}
