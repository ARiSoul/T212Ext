using System.Globalization;
using ApexCharts;
using Arisoul.T212Ext.Core;
using Arisoul.T212Ext.Web;
using Arisoul.T212Ext.Web.Components;
using Arisoul.T212Ext.Web.Services;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddT212ExtCore(builder.Configuration);
builder.Services.AddT212ExtSyncWorker();
builder.Services.AddHealthChecks();

builder.Services.AddLocalization();
builder.Services.AddApexCharts();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<BrowserStorage>();
builder.Services.AddScoped<AccountContext>();

var app = builder.Build();

await app.Services.InitializeT212ExtAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapGet("/language", (string culture, string? returnUrl, HttpContext context) =>
{
    context.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(Languages.Resolve(culture))),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = true });

    // Local URLs only: the language link must not become an open redirect.
    return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') ? "/" : returnUrl);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
