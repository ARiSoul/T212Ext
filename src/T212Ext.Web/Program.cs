using System.Globalization;
using Arisoul.T212Ext.Core;
using Arisoul.T212Ext.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddT212ExtCore(builder.Configuration);
builder.Services.AddT212ExtSyncWorker();
builder.Services.AddHealthChecks();

// Numbers and dates follow one culture for everyone (the UI text is English for now).
var culture = CultureInfo.GetCultureInfo(builder.Configuration["Ui:Culture"] ?? "pt-PT");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var app = builder.Build();

await app.Services.InitializeT212ExtAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
