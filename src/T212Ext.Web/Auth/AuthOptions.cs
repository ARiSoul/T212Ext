namespace Arisoul.T212Ext.Web.Auth;

/// <summary>
/// The people who may sign in, from configuration (<c>Auth:Users</c>). The app holds one person's
/// finances, so there is no sign-up and no user table: users live in the server's environment.
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    public List<AuthUser> Users { get; set; } = [];

    /// <summary>How long a sign-in lasts without use; each request within it extends it.</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>Sign-in attempts allowed per address per minute.</summary>
    public int AttemptsPerMinute { get; set; } = 5;

    /// <summary>
    /// Without users the app is open, which is only allowed in Development (a local run);
    /// anywhere else it refuses to start.
    /// </summary>
    public bool Enabled => Users.Count > 0;
}

public sealed class AuthUser
{
    public string Username { get; set; } = "";

    /// <summary>An ASP.NET Core <c>PasswordHasher</c> hash, made with <c>dotnet T212Ext.Web.dll hash-password</c>.</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>Optional Base32 secret for an authenticator app (RFC 6238), made with <c>new-totp-secret</c>.</summary>
    public string? TotpSecret { get; set; }
}
