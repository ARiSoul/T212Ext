using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Security;
using Arisoul.T212Ext.Core.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arisoul.T212Ext.Core.Tests;

[Collection(DatabaseCollection.Name)]
public class SignInHistoryTests(DatabaseFixture database)
{
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    private SignInHistory History() => new(database, _time, NullLogger<SignInHistory>.Instance);

    // Each test its own address: the table is shared.
    private static string NewAddress() => "10.0." + Random.Shared.Next(256) + "." + Random.Shared.Next(256) + "#" + Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task Attempts_are_listed_newest_first_and_an_unknown_username_is_not_kept()
    {
        var address = NewAddress();
        await History().RecordAsync("ricardo", SignInOutcome.Failed, address, "Mozilla/5.0");
        _time.Now = _time.Now.AddMinutes(1);
        await History().RecordAsync("hunter2-typed-in-the-wrong-field", SignInOutcome.UnknownUser, address, null);
        _time.Now = _time.Now.AddMinutes(1);
        await History().RecordAsync("ricardo", SignInOutcome.Succeeded, address, new string('x', 500));

        var rows = (await History().ListAsync()).Where(r => r.Address == address).ToList();

        Assert.Equal([SignInOutcome.Succeeded, SignInOutcome.UnknownUser, SignInOutcome.Failed], rows.Select(r => r.Outcome));
        Assert.Null(rows[1].Username);
        Assert.Equal(SignInHistory.MaxUserAgent, rows[0].UserAgent!.Length);
    }

    [Fact]
    public async Task Attempts_older_than_90_days_are_removed()
    {
        var old = NewAddress();
        var recent = NewAddress();
        await History().RecordAsync("ricardo", SignInOutcome.Succeeded, old, null);

        _time.Now = _time.Now.AddDays(SignInHistory.KeepDays + 1);
        await History().RecordAsync("ricardo", SignInOutcome.Succeeded, recent, null);

        var addresses = (await History().ListAsync()).Select(r => r.Address).ToList();
        Assert.DoesNotContain(old, addresses);
        Assert.Contains(recent, addresses);
    }
}
