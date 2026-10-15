using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Web.Auth;

/// <summary>
/// Checks a username, password and (when the user has a secret) authenticator code. A failure does
/// not say which part was wrong.
/// </summary>
public sealed class SignInService(IOptions<AuthOptions> options, TimeProvider time)
{
    private static readonly PasswordHasher<AuthUser> Hasher = new();

    // A hash of a random password, checked when the username is unknown, so the answer takes
    // about as long as for a known user and does not reveal which usernames exist.
    private static readonly string DummyHash = Hasher.HashPassword(new AuthUser(), Guid.NewGuid().ToString("N"));

    // The last code step each user signed in with: a code seen once is refused afterwards.
    private readonly ConcurrentDictionary<string, long> _lastStep = new(StringComparer.OrdinalIgnoreCase);

    public bool RequiresCode => options.Value.Users.Any(u => !string.IsNullOrEmpty(u.TotpSecret));

    public AuthUser? Check(string? username, string? password, string? code) => Verify(username, password, code).User;

    /// <summary>
    /// The user when all is right; otherwise null, with <see cref="SignInCheck.Known"/> saying whether the
    /// username exists (for the sign-in history only: the person signing in is never told).
    /// </summary>
    public SignInCheck Verify(string? username, string? password, string? code)
    {
        var user = options.Value.Users.FirstOrDefault(u => string.Equals(u.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase));
        var passwordOk = Hasher.VerifyHashedPassword(user ?? new AuthUser(), user?.PasswordHash ?? DummyHash, password ?? "")
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        if (user is null)
            return new SignInCheck(null, null);
        if (!passwordOk)
            return new SignInCheck(null, user);

        if (string.IsNullOrEmpty(user.TotpSecret))
            return new SignInCheck(user, user);

        if (Totp.Match(user.TotpSecret, code, time.GetUtcNow()) is not { } step)
            return new SignInCheck(null, user);

        // Replay: only a step later than the last one used signs in.
        var accepted = false;
        _lastStep.AddOrUpdate(user.Username, _ => { accepted = true; return step; },
            (_, last) => { accepted = step > last; return accepted ? step : last; });
        return new SignInCheck(accepted ? user : null, user);
    }

    public static string HashPassword(string password) => Hasher.HashPassword(new AuthUser(), password);

    /// <summary>
    /// Where to go after signing in: only a path on this site, so the login link cannot be used
    /// to send someone elsewhere ("//evil.example" and "/\evil.example" are other sites).
    /// </summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is { Length: > 0 } && returnUrl[0] == '/' && (returnUrl.Length == 1 || returnUrl[1] is not ('/' or '\\'))
            ? returnUrl
            : "/";
}

/// <param name="User">Signed in: everything was right.</param>
/// <param name="Known">The configured user the username names, right or wrong; null when there is none.</param>
public sealed record SignInCheck(AuthUser? User, AuthUser? Known);
