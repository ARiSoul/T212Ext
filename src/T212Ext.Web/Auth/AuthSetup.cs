using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Arisoul.T212Ext.Core.Domain;
using Arisoul.T212Ext.Core.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Web.Auth;

public static class AuthSetup
{
    public const string LoginPath = "/login";
    private const string LoginRateLimit = "login";
    private const string FingerprintClaim = "t212ext:pwd";

    public static WebApplicationBuilder AddT212ExtAuth(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<AuthOptions>().Bind(builder.Configuration.GetSection(AuthOptions.Section));
        services.AddSingleton<SignInService>();

        // Checked here, before the app touches the database: a server without users does not start.
        var auth = builder.Configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
        if (Validate(auth, builder.Environment.IsDevelopment()) is { } failure)
            throw new InvalidOperationException(failure);

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.Cookie.Name = ".T212Ext.Auth";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                cookie.ExpireTimeSpan = auth.SessionLifetime;
                cookie.SlidingExpiration = true;
                cookie.LoginPath = LoginPath;
                cookie.ReturnUrlParameter = "returnUrl";
                // A user removed from the configuration, or whose password changed, is signed out.
                cookie.Events.OnValidatePrincipal = context =>
                {
                    var users = context.HttpContext.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.Users;
                    var user = users.FirstOrDefault(u => u.Username == context.Principal?.Identity?.Name);
                    if (user is null || Fingerprint(user) != context.Principal?.FindFirstValue(FingerprintClaim))
                        context.RejectPrincipal();
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options =>
        {
            // Every endpoint needs a signed-in user unless it says otherwise.
            if (auth.Enabled)
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
        services.AddCascadingAuthenticationState();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = auth.AttemptsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Redirect($"{LoginPath}?error=throttled");
                return ValueTask.CompletedTask;
            };
        });

        return builder;
    }

    public static WebApplication MapT212ExtAuth(this WebApplication app)
    {
        var auth = app.Services.GetRequiredService<IOptions<AuthOptions>>().Value;
        if (!auth.Enabled)
        {
            app.Logger.LogWarning("No users in Auth:Users: the app is open to anyone who reaches it (allowed in Development only).");
            return app;
        }

        app.MapPost("/auth/login", async ([FromForm] LoginForm form, HttpContext context, SignInService signIn, SignInHistory history, ILogger<SignInService> logger) =>
            {
                var returnUrl = SignInService.SafeReturnUrl(form.ReturnUrl);
                var check = signIn.Verify(form.Username, form.Password, form.Code);
                var address = context.Connection.RemoteIpAddress?.ToString();
                var userAgent = context.Request.Headers.UserAgent.ToString();
                if (check.User is not { } user)
                {
                    logger.LogWarning("Failed sign-in for {Username} from {Address}", form.Username, address);
                    await history.RecordAsync(check.Known?.Username, check.Known is null ? SignInOutcome.UnknownUser : SignInOutcome.Failed, address, userAgent);
                    return Results.LocalRedirect($"{LoginPath}?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
                }

                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, user.Username), new Claim(FingerprintClaim, Fingerprint(user))],
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
                logger.LogInformation("{Username} signed in from {Address}", user.Username, address);
                await history.RecordAsync(user.Username, SignInOutcome.Succeeded, address, userAgent);
                return Results.LocalRedirect(returnUrl);
            })
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimit);

        app.MapPost("/auth/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync();
                return Results.LocalRedirect(LoginPath);
            })
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        return app;
    }

    /// <summary>Why the options cannot be used, or null when they can.</summary>
    public static string? Validate(AuthOptions options, bool isDevelopment)
    {
        if (!options.Enabled && !isDevelopment)
            return "Auth:Users is empty. Outside Development the app needs at least one user (see docs/DEPLOYMENT.md).";

        foreach (var user in options.Users)
        {
            if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.PasswordHash))
                return "Every user in Auth:Users needs a Username and a PasswordHash.";
            if (!string.IsNullOrEmpty(user.TotpSecret))
            {
                try
                {
                    if (Totp.Base32Decode(user.TotpSecret).Length < 10)
                        return $"The TotpSecret of {user.Username} is too short (at least 16 Base32 characters).";
                }
                catch (FormatException)
                {
                    return $"The TotpSecret of {user.Username} is not Base32.";
                }
            }
        }

        if (options.Users.GroupBy(u => u.Username.Trim(), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return "A username appears more than once in Auth:Users.";
        return null;
    }

    // Part of the password hash's digest: changing the password changes it, ending every session.
    private static string Fingerprint(AuthUser user) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.PasswordHash + "|" + user.TotpSecret)))[..16];

    public sealed class LoginForm
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Code { get; set; }
        public string? ReturnUrl { get; set; }
    }
}
