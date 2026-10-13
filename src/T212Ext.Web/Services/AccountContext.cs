using Arisoul.T212Ext.Core.Queries;

namespace Arisoul.T212Ext.Web.Services;

/// <summary>
/// The account the pages show (null: all), chosen in the top bar and remembered in the browser.
/// Pages listen to <see cref="Changed"/> and reload.
/// </summary>
public sealed class AccountContext(OverviewQueries overview, BrowserStorage storage)
{
    private const string StorageKey = "t212ext.account";

    public IReadOnlyList<AccountOverview> Accounts { get; private set; } = [];
    public int? AccountId { get; private set; }
    public bool IsReady { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (IsReady)
            return;

        Accounts = await overview.GetAccountsAsync();
        var saved = await storage.GetAsync<int?>(StorageKey);
        AccountId = saved is { } id && Accounts.Any(a => a.Id == id) ? id : null;
        IsReady = true;
        Changed?.Invoke();
    }

    public async Task SelectAsync(int? accountId)
    {
        AccountId = accountId;
        await storage.SetAsync(StorageKey, accountId);
        Changed?.Invoke();
    }
}
