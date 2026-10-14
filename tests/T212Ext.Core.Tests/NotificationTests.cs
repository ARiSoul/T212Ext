using System.Globalization;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Notifications;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Tests;

[Collection(DatabaseCollection.Name)]
public class NotifierTests : IAsyncLifetime
{
    // Far from the other tests' dividends: the notifier looks at every account.
    private static readonly DateTimeOffset Now = new(2031, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly DatabaseFixture _database;
    private readonly ManualTimeProvider _time = new(Now);
    private readonly FakePushSender _sender = new();
    private BrokerAccount _account = null!;

    public NotifierTests(DatabaseFixture database) => _database = database;

    // The state and the devices are one per database: every test starts from none.
    public async Task InitializeAsync()
    {
        await using var db = _database.CreateDbContext();
        await db.NotificationStates.ExecuteDeleteAsync();
        await db.PushDevices.ExecuteDeleteAsync();
        _account = await _database.CreateAccountAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private PushDevices Devices => Push.Devices(_database, _sender, _time);
    private Notifier Notifier => new(_database, Devices, new FakePushTexts(), _time);

    private async Task AddDividendAsync(DateTimeOffset paidOn, decimal amount, string name = "Apple", string currency = "EUR")
    {
        await using var db = _database.CreateDbContext();
        db.Dividends.Add(new Dividend
        {
            AccountId = _account.Id, Reference = Guid.NewGuid().ToString("N"), Sources = DataSources.Api, Ticker = "AAPL_US_EQ",
            InstrumentName = name, Type = "ORDINARY", PaidOn = paidOn, Quantity = 1, Amount = amount, Currency = currency,
        });
        await db.SaveChangesAsync();
    }

    private Task AddDeviceAsync(string culture = "pt-PT", string endpoint = "https://fcm.googleapis.com/fcm/send/one") =>
        Devices.RegisterAsync("ricardo", Push.Subscription(endpoint), "Android · Chrome", CultureInfo.GetCultureInfo(culture));

    [Fact]
    public async Task The_first_run_only_takes_note_then_new_dividends_are_announced_once()
    {
        await AddDeviceAsync();
        await AddDividendAsync(Now.AddDays(-1), 1);

        await Notifier.RunAsync(default);
        Assert.Empty(_sender.Sent);   // already there when notifications started

        await AddDividendAsync(Now.AddHours(-2), 0.82m);
        await AddDividendAsync(Now.AddHours(-1), 0.65m, "Microsoft");
        var result = await Notifier.RunAsync(default);

        var (_, message) = Assert.Single(_sender.Sent);
        Assert.Equal("pt-PT dividends 2", message.GetProperty("title").GetString());
        Assert.Equal("Apple 0.82 EUR, Microsoft 0.65 EUR", message.GetProperty("body").GetString());
        Assert.Equal("/dividends", message.GetProperty("url").GetString());
        Assert.Equal(1, result.Added);

        await Notifier.RunAsync(default);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task Old_dividends_a_report_brings_are_not_announced()
    {
        await AddDeviceAsync();
        await Notifier.RunAsync(default);

        await AddDividendAsync(Now.AddYears(-2), 3);
        await AddDividendAsync(Now.AddDays(-8), 2);
        await Notifier.RunAsync(default);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Without_devices_nothing_is_sent_and_turning_one_on_later_does_not_flood_it()
    {
        await Notifier.RunAsync(default);
        await AddDividendAsync(Now.AddHours(-1), 1);
        await Notifier.RunAsync(default);

        await AddDeviceAsync();
        await Notifier.RunAsync(default);

        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task The_month_before_is_summed_up_once_when_it_is_over()
    {
        // A month of its own: the other tests' dividends are in June 2031.
        _time.Now = new DateTimeOffset(2033, 3, 10, 12, 0, 0, TimeSpan.Zero);
        await AddDeviceAsync("en-GB");
        await Notifier.RunAsync(default);   // March: takes note that February is done

        await AddDividendAsync(new DateTimeOffset(2033, 3, 3, 0, 0, 0, TimeSpan.Zero), 10);
        await AddDividendAsync(new DateTimeOffset(2033, 3, 20, 0, 0, 0, TimeSpan.Zero), 5, currency: "GBP");
        await AddDividendAsync(new DateTimeOffset(2032, 3, 10, 0, 0, 0, TimeSpan.Zero), 8);
        _sender.Sent.Clear();

        _time.Now = new DateTimeOffset(2033, 4, 1, 6, 0, 0, TimeSpan.Zero);
        await Notifier.RunAsync(default);
        await Notifier.RunAsync(default);

        var (_, message) = Assert.Single(_sender.Sent, s => s.Message.GetProperty("title").GetString()!.Contains("summary"));
        Assert.Equal("en-GB summary 2033-03", message.GetProperty("title").GetString());
        Assert.Equal("10 EUR (before 8), 5 GBP (before none) in 2", message.GetProperty("body").GetString());
        Assert.Equal("summary-2033-03", message.GetProperty("tag").GetString());
    }

    [Fact]
    public async Task Each_device_gets_its_language_and_a_device_the_push_service_forgot_is_removed()
    {
        await AddDeviceAsync("pt-PT", "https://fcm.googleapis.com/fcm/send/pt");
        await AddDeviceAsync("es-ES", "https://web.push.apple.com/es");
        await AddDeviceAsync("en-GB", "https://updates.push.services.mozilla.com/gone");
        _sender.Gone.Add("https://updates.push.services.mozilla.com/gone");
        await Notifier.RunAsync(default);

        await AddDividendAsync(Now.AddHours(-1), 1);
        await Notifier.RunAsync(default);

        Assert.Equal(["es-ES dividends 1", "pt-PT dividends 1"], _sender.Sent.Select(s => s.Message.GetProperty("title").GetString()).Order());
        var devices = await Devices.ListAsync();
        Assert.Equal(2, devices.Count);
        Assert.All(devices, d => Assert.Equal(Now, d.LastUsedAt));
    }
}

[Collection(DatabaseCollection.Name)]
public class PushDevicesTests(DatabaseFixture database) : IAsyncLifetime
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2031, 6, 15, 12, 0, 0, TimeSpan.Zero));
    private readonly FakePushSender _sender = new();

    public async Task InitializeAsync()
    {
        await using var db = database.CreateDbContext();
        await db.PushDevices.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private PushDevices Devices(PushOptions? options = null) => Push.Devices(database, _sender, _time, options);

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://jmt17.google.com/fcm/send/abc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://wns2-db5p.notify.windows.com/w/?token=abc", true)]
    [InlineData("https://web.push.apple.com/abc", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]
    [InlineData("https://googleapis.com.evil.example/abc", false)]
    [InlineData("https://evilgoogleapis.com/abc", false)]
    [InlineData("https://localhost/abc", false)]
    [InlineData("https://10.0.0.5/abc", false)]
    [InlineData("not a url", false)]
    public void Only_the_browsers_push_services_are_accepted(string endpoint, bool accepted) =>
        Assert.Equal(accepted, PushDevices.IsPushServiceUrl(endpoint));

    [Fact]
    public async Task The_same_browser_subscribing_again_is_one_device_with_its_new_keys_and_language()
    {
        await Devices().RegisterAsync("ricardo", Push.Subscription(), "Android · Chrome", CultureInfo.GetCultureInfo("pt-PT"));
        await Devices().RegisterAsync("ricardo", Push.Subscription() with { Auth = "new-auth" }, "Android · Chrome (app)", CultureInfo.GetCultureInfo("en-GB"));

        await using var db = database.CreateDbContext();
        var device = await db.PushDevices.SingleAsync();
        Assert.Equal(("new-auth", "en-GB", "Android · Chrome (app)"), (device.Auth, device.Culture, device.Label));
        Assert.Equal(PushDevices.Hash(Push.Subscription().Endpoint), device.EndpointHash);
    }

    [Fact]
    public async Task Unknown_services_or_a_server_without_keys_are_refused()
    {
        Assert.False(await Devices().RegisterAsync("ricardo", Push.Subscription("https://evil.example/push"), "x", CultureInfo.InvariantCulture));
        Assert.False(await Devices(new PushOptions()).RegisterAsync("ricardo", Push.Subscription(), "x", CultureInfo.InvariantCulture));
        Assert.Empty(await Devices().ListAsync());
    }

    [Fact]
    public async Task Removing_a_device_leaves_the_others()
    {
        await Devices().RegisterAsync("ricardo", Push.Subscription("https://fcm.googleapis.com/fcm/send/a"), "A", CultureInfo.InvariantCulture);
        await Devices().RegisterAsync("ricardo", Push.Subscription("https://fcm.googleapis.com/fcm/send/b"), "B", CultureInfo.InvariantCulture);

        await Devices().RemoveAsync(PushDevices.Hash("https://fcm.googleapis.com/fcm/send/a"));

        Assert.Equal(["B"], (await Devices().ListAsync()).Select(d => d.Label));
    }

    [Fact]
    public async Task A_test_goes_to_that_device_only_in_its_language()
    {
        await Devices().RegisterAsync("ricardo", Push.Subscription("https://fcm.googleapis.com/fcm/send/a"), "A", CultureInfo.GetCultureInfo("es-ES"));
        await Devices().RegisterAsync("ricardo", Push.Subscription("https://fcm.googleapis.com/fcm/send/b"), "B", CultureInfo.GetCultureInfo("pt-PT"));

        Assert.True(await Devices().SendTestAsync(PushDevices.Hash("https://fcm.googleapis.com/fcm/send/a")));

        var (endpoint, message) = Assert.Single(_sender.Sent);
        Assert.Equal("https://fcm.googleapis.com/fcm/send/a", endpoint);
        Assert.Equal("es-ES test", message.GetProperty("title").GetString());
    }
}
