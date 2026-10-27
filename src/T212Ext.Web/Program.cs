using System.Globalization;
using ApexCharts;
using Arisoul.T212Ext.Core;
using Arisoul.T212Ext.Core.Notifications;
using Arisoul.T212Ext.Web;
using Arisoul.T212Ext.Web.Auth;
using Arisoul.T212Ext.Web.Components;
using Arisoul.T212Ext.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;

if (AuthCommands.IsCommand(args))
    return AuthCommands.Run(args, Console.In, Console.Out, Console.Error);
if (AuthCommands.IsUnknownCommand(args))
    return AuthCommands.Unknown(args, Console.Error);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddT212ExtCore(builder.Configuration);
builder.Services.AddT212ExtSyncWorker();
builder.Services.AddHealthChecks();
builder.AddT212ExtAuth();

// Sign-in and language cookies are encrypted with these keys: on the server they live on a
// volume, so a restart or a new image does not sign everyone out.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(keysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("T212Ext")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.AddLocalization();
builder.Services.AddApexCharts();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<BrowserStorage>();
builder.Services.AddScoped<AccountContext>();
builder.Services.AddSingleton<IPushTexts, PushTexts>();

var app = builder.Build();

await app.Services.InitializeT212ExtAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// No framing (the sign-in page cannot be overlaid by another site), no MIME sniffing, no full
// URLs (which can carry a year or an instrument) sent to other sites, and a Content-Security-Policy.
app.UseSecurityHeaders();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// The language comes from the cookie the language menu sets; without it, Ui:DefaultCulture.
var defaultCulture = Languages.Resolve(app.Configuration["Ui:DefaultCulture"]);
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
    SupportedCultures = [.. Languages.All.Select(l => CultureInfo.GetCultureInfo(l.Culture))],
    SupportedUICultures = [.. Languages.All.Select(l => CultureInfo.GetCultureInfo(l.Culture))],
    RequestCultureProviders = [new CookieRequestCultureProvider()],
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

// The sign-in page needs its styles and scripts before anyone is signed in.
app.MapStaticAssets().AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapT212ExtAuth();
app.MapPwa();
app.MapLogos();

app.MapGet("/language", (string culture, string? returnUrl, HttpContext context) =>
{
    context.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(Languages.Resolve(culture))),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = true, Secure = context.Request.IsHttps });

    // Local URLs only: the language link must not become an open redirect.
    return Results.LocalRedirect(SignInService.SafeReturnUrl(returnUrl));
}).AllowAnonymous();

app.MapRazorComponents<App>()
    // SecurityHeaders already sends frame-ancestors 'none'; Blazor's own 'self' policy is not needed.
    .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = null);

app.Run();
return 0;
