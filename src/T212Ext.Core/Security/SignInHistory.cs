using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Arisoul.T212Ext.Core.Security;

public sealed record SignInRow(long Id, DateTimeOffset At, string? Username, SignInOutcome Outcome, string? Address, string? UserAgent);

/// <summary>
/// The sign-in attempts of the last <see cref="KeepDays"/> days, at most <see cref="MaxRows"/>: who,
/// when, from where, and whether it worked. Recording never stops a sign-in: a failure is logged.
/// </summary>
public sealed class SignInHistory(IDbContextFactory<T212ExtDbContext> dbFactory, TimeProvider time, ILogger<SignInHistory> logger)
{
    public const int KeepDays = 90;

    /// <summary>A ceiling, should someone try thousands of times within the 90 days (the rate limit is per address).</summary>
    public const int MaxRows = 5000;

    public const int MaxUserAgent = 300;

    public async Task RecordAsync(string? username, SignInOutcome outcome, string? address, string? userAgent, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var now = time.GetUtcNow();
            db.SignInAttempts.Add(new SignInAttempt
            {
                At = now,
                Username = outcome == SignInOutcome.UnknownUser ? null : username,
                Outcome = outcome,
                Address = Truncate(address, 64),
                UserAgent = Truncate(userAgent, MaxUserAgent),
            });
            await db.SaveChangesAsync(cancellationToken);

            var expired = now.AddDays(-KeepDays);
            await db.SignInAttempts.Where(a => a.At < expired).ExecuteDeleteAsync(cancellationToken);
            var oldestKept = await db.SignInAttempts.OrderByDescending(a => a.Id).Skip(MaxRows - 1).Select(a => (long?)a.Id).FirstOrDefaultAsync(cancellationToken);
            if (oldestKept is { } id)
                await db.SignInAttempts.Where(a => a.Id < id).ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not record a sign-in attempt ({Outcome}) from {Address}", outcome, address);
        }
    }

    public async Task<IReadOnlyList<SignInRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SignInAttempts.AsNoTracking()
            .OrderByDescending(a => a.At).ThenByDescending(a => a.Id)
            .Select(a => new SignInRow(a.Id, a.At, a.Username, a.Outcome, a.Address, a.UserAgent))
            .ToListAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int length) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= length ? value : value[..length];
}
