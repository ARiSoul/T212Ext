using Arisoul.T212Ext.Web.Resources;
using Arisoul.T212Ext.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace Arisoul.T212Ext.Web.Components.Shared;

/// <summary>
/// A page whose data depends on the account chosen in the top bar: it loads once the choice is
/// known, and again whenever it changes or a sync ends (<see cref="ReloadAsync"/>).
/// </summary>
public abstract class AccountPage : ComponentBase, IDisposable
{
    [Inject] protected AccountContext AccountContext { get; set; } = default!;
    [Inject] protected IStringLocalizer<SharedResource> L { get; set; } = default!;

    protected int? AccountId => AccountContext.AccountId;

    /// <summary>True when more than one account is configured: grids show the account column.</summary>
    protected bool SeveralAccounts => AccountContext.Accounts.Count > 1;

    protected bool Loading { get; private set; } = true;

    protected abstract Task LoadAsync();

    protected override async Task OnInitializedAsync()
    {
        AccountContext.Changed += OnAccountChanged;
        if (AccountContext.IsReady)
            await ReloadAsync();
    }

    protected async Task ReloadAsync()
    {
        Loading = true;
        await LoadAsync();
        Loading = false;
    }

    private void OnAccountChanged() => InvokeAsync(async () =>
    {
        await ReloadAsync();
        StateHasChanged();
    });

    public virtual void Dispose()
    {
        AccountContext.Changed -= OnAccountChanged;
        GC.SuppressFinalize(this);
    }
}
