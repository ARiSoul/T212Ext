using Arisoul.T212Ext.Core.Analytics;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Queries;
using Arisoul.T212Ext.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Notifications;

/// <summary>
/// After each sync: announces the dividends that arrived since the last run, once a month sums up the
/// month before, and on Monday morning lists the payments expected that week. What was announced is
/// kept in <see cref="NotificationState"/>, so nothing is announced twice. The first run only takes
/// note of where things stand: turning notifications on never floods the phone with the whole history.
/// Each device gets only the topics it chose (<see cref="PushTopic"/>).
/// </summary>
public sealed class Notifier(IDbContextFactory<T212ExtDbContext> dbFactory, PushDevices devices, IPushTexts texts, AnalyticsQueries analytics, TimeProvider time)
{
    /// <summary>The week's notice goes out from this time on Monday (UTC): morning in Europe, not the middle of the night.</summary>
    public static readonly TimeSpan WeekNoticeAt = TimeSpan.FromHours(6);

    /// <summary>Only dividends paid this recently are announced (a report can bring old ones).</summary>
    public static readonly TimeSpan Recent = TimeSpan.FromDays(7);

    private const int StateId = 1;

    public bool IsConfigured => devices.IsConfigured;

    public async Task<JobResult> RunAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var state = await db.NotificationStates.FindAsync([StateId], cancellationToken);
        if (state is null)
        {
            state = new NotificationState { Id = StateId };
            db.NotificationStates.Add(state);
        }

        var hasDevices = await db.PushDevices.AnyAsync(cancellationToken);
        var now = time.GetUtcNow();
        var sent = 0;

        // New dividends.
        var maxId = await db.Dividends.MaxAsync(d => (long?)d.Id, cancellationToken);
        if (state.LastDividendId is { } last && maxId > last && hasDevices)
        {
            var since = now - Recent;
            var fresh = await db.Dividends.AsNoTracking()
                .Where(d => d.Id > last && d.PaidOn >= since)
                .OrderBy(d => d.PaidOn)
                .Select(d => new { d.InstrumentName, d.Ticker, d.Amount, d.Currency, d.PaidOn })
                .ToListAsync(cancellationToken);

            if (fresh.Count > 0)
            {
                var notices = fresh.Select(d => new DividendNotice(d.InstrumentName ?? d.Ticker.Split('_')[0], d.Amount, d.Currency, d.PaidOn)).ToList();
                sent += await devices.SendToAllAsync(PushTopic.Dividends, culture =>
                {
                    var text = texts.NewDividends(culture, notices);
                    return new PushMessage(text.Title, text.Body, "/dividends", $"dividends-{maxId}");
                }, cancellationToken);
            }
        }
        state.LastDividendId = maxId ?? state.LastDividendId ?? 0;

        // The month before, once it is over.
        var previousMonth = new DateOnly(now.Year, now.Month, 1).AddMonths(-1);
        if (state.LastSummaryMonth is null)
        {
            state.LastSummaryMonth = previousMonth;
        }
        else if (state.LastSummaryMonth < previousMonth)
        {
            var month = await MonthAsync(db, previousMonth, cancellationToken);
            if (month.Count > 0 && hasDevices)
            {
                sent += await devices.SendToAllAsync(PushTopic.Summary, culture =>
                {
                    var text = texts.MonthlySummary(culture, month);
                    return new PushMessage(text.Title, text.Body, "/dividends", $"summary-{previousMonth:yyyy-MM}");
                }, cancellationToken);
            }
            state.LastSummaryMonth = previousMonth;
        }

        // This week's expected payments, once, from Monday morning.
        var monday = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-(((int)now.UtcDateTime.DayOfWeek + 6) % 7));
        var weekDue = now >= new DateTimeOffset(monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) + WeekNoticeAt;
        if (state.LastWeekNotice is null)
        {
            state.LastWeekNotice = weekDue ? monday : monday.AddDays(-7);
        }
        else if (weekDue && state.LastWeekNotice < monday)
        {
            var week = await WeekAsync(monday, cancellationToken);
            if (week.Payments.Count > 0 && hasDevices)
            {
                sent += await devices.SendToAllAsync(PushTopic.Week, culture =>
                {
                    var text = texts.WeekAhead(culture, week);
                    return new PushMessage(text.Title, text.Body, "/calendar", $"week-{monday:yyyy-MM-dd}");
                }, cancellationToken);
            }
            state.LastWeekNotice = monday;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new JobResult(sent, 0);
    }

    /// <summary>The payments the calendar expects from Monday to Sunday (estimated, or due any day now), in every currency, all accounts.</summary>
    internal async Task<WeekNotice> WeekAsync(DateOnly monday, CancellationToken cancellationToken)
    {
        var payments = new List<ExpectedNotice>();
        foreach (var currency in await analytics.GetCurrenciesAsync(null, cancellationToken))
        {
            var dashboard = await analytics.GetDashboardAsync(null, currency, cancellationToken);
            payments.AddRange((dashboard?.Upcoming ?? [])
                .Where(e => e.Status != ExpectedStatus.Late && e.ExpectedOn >= monday && e.ExpectedOn < monday.AddDays(7))
                .Select(e => new ExpectedNotice(e.Name ?? e.Ticker.Split('_')[0], e.Amount, currency, e.ExpectedOn)));
        }

        return new WeekNotice(
            monday,
            [.. payments.OrderByDescending(p => p.Amount)],
            [.. payments.GroupBy(p => p.Currency).Select(g => (g.Key, g.Sum(p => p.Amount))).OrderByDescending(t => t.Item2)]);
    }

    /// <summary>A month's dividends per currency (UTC months, as the dividend pages count them), with the same month a year before.</summary>
    internal static async Task<MonthNotice> MonthAsync(T212ExtDbContext db, DateOnly month, CancellationToken cancellationToken)
    {
        static DateTimeOffset Start(DateOnly m) => new(m.Year, m.Month, 1, 0, 0, 0, TimeSpan.Zero);

        async Task<List<(string Currency, decimal Amount, int Count)>> TotalsAsync(DateOnly m)
        {
            var from = Start(m);
            var to = Start(m.AddMonths(1));
            var rows = await db.Dividends.AsNoTracking()
                .Where(d => d.PaidOn >= from && d.PaidOn < to)
                .GroupBy(d => d.Currency)
                .Select(g => new { Currency = g.Key, Amount = g.Sum(d => d.Amount), Count = g.Count() })
                .ToListAsync(cancellationToken);
            return [.. rows.Select(r => (r.Currency, r.Amount, r.Count))];
        }

        var current = await TotalsAsync(month);
        var yearBefore = (await TotalsAsync(month.AddYears(-1))).ToDictionary(t => t.Currency, t => t.Amount);

        return new MonthNotice(
            month,
            [.. current.OrderByDescending(t => t.Amount).Select(t => (t.Currency, t.Amount, yearBefore.TryGetValue(t.Currency, out var before) ? before : (decimal?)null))],
            current.Sum(t => t.Count));
    }
}
