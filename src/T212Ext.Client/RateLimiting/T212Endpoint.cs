namespace Arisoul.T212Ext.Client.RateLimiting;

/// <summary>An API endpoint and its documented rate limit period (used when a 429 has no reset header).</summary>
public sealed record T212Endpoint(string Key, string Path, TimeSpan Period)
{
    public static readonly T212Endpoint AccountSummary = new("account-summary", "/api/v0/equity/account/summary", TimeSpan.FromSeconds(5));
    public static readonly T212Endpoint Positions = new("positions", "/api/v0/equity/positions", TimeSpan.FromSeconds(1));
    public static readonly T212Endpoint Instruments = new("instruments", "/api/v0/equity/metadata/instruments", TimeSpan.FromSeconds(50));
    public static readonly T212Endpoint Dividends = new("dividends", "/api/v0/equity/history/dividends", TimeSpan.FromMinutes(1));
    public static readonly T212Endpoint Orders = new("orders", "/api/v0/equity/history/orders", TimeSpan.FromMinutes(1));
    public static readonly T212Endpoint Transactions = new("transactions", "/api/v0/equity/history/transactions", TimeSpan.FromMinutes(1));
    public static readonly T212Endpoint RequestReport = new("exports-post", "/api/v0/equity/history/exports", TimeSpan.FromSeconds(30));
    public static readonly T212Endpoint ListReports = new("exports-get", "/api/v0/equity/history/exports", TimeSpan.FromMinutes(1));
}
