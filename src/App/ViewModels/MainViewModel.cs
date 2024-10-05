using Arisoul.T212.Client;
using Arisoul.T212.Client.Services;
using Arisoul.T212.Models;

namespace Arisoul.T212.App.ViewModels;

public partial class MainViewModel
    : BaseViewModel
{
    [ObservableProperty] private Account _account;
    [ObservableProperty] private AccountCash _accountCash;
    [ObservableProperty] private bool _showAccountData;
    [ObservableProperty] private bool _showAccountCash;
    [ObservableProperty] private bool _isRefreshingCash;
    [ObservableProperty] private bool _cashHasError;

    private readonly AccountRestService _accountRestService;
    private readonly IT212ClientOptions _clientOptions;

    private bool _accountDataLoadedFirstTime;
    private bool _accountCashLoadedFirstTime;

    public MainViewModel(IServiceProvider serviceProvider)
    {
        _accountRestService = serviceProvider.GetRequiredService<AccountRestService>();
        _clientOptions = serviceProvider.GetRequiredService<IT212ClientOptions>();
        _clientOptions.LoadOptions();
    }

    [RelayCommand]
    private async Task LoadAccountAsync()
    {
        if (!_clientOptions.IsInitiated)
        {
            await Dialogs.ShowMessage("Please configure the app first.");
            await Shell.Current.GoToAsync("//SettingsPage");
            return;
        }

        if (!_clientOptions.HasChanged)
        {
            var accountSaved = Preferences.Get(nameof(T212.Models.Account), string.Empty);

            if (!string.IsNullOrWhiteSpace(accountSaved))
            {
                Account = JsonSerializer.Deserialize<Account>(accountSaved)
                    ?? throw new InvalidOperationException("Failed to deserialize Account.");
                ShowAccountData = true;
                _accountDataLoadedFirstTime = true;
                return;
            }
        }

        if (_accountDataLoadedFirstTime && !_clientOptions.HasChanged)
            return;

        IsRefreshing = true;
        HasError = false;

        try
        {
            var hasConnection = await CheckConnectivity();
            if (!hasConnection)
            {
                HasError = true;
                return;
            }

            var result = await _accountRestService.GetAccountMetadataAsync();

            if (result.Success)
            {
                Account = result.Value!;
                Preferences.Set(nameof(T212.Models.Account), JsonSerializer.Serialize(Account));
                _clientOptions.SaveOptions(false);
                _accountDataLoadedFirstTime = true;
                ShowAccountData = true;
            }
            else
            {
                HasError = true;
                await Dialogs.ShowError(result.Message);
            }
        }
        catch
        {
            HasError = true;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task LoadAccountCashAsync()
    {
        IsRefreshingCash = true;
        CashHasError = false;

        try
        {
            var hasConnection = await CheckConnectivity();
            if (!hasConnection)
            {
                CashHasError = true;
                return;
            }

            var result = await _accountRestService.GetAccountCashAsync();

            if (result.Success)
            {
                AccountCash = result.Value!;
                _accountCashLoadedFirstTime = true;
                ShowAccountCash = true;
            }
            else
            {
                CashHasError = true;
                await Dialogs.ShowError(result.Message);
            }
        }
        catch
        {
            CashHasError = true;
        }
        finally
        {
            IsRefreshingCash = false;
        }
    }

    [RelayCommand]
    private void HideAccountCash()
    {
        ShowAccountCash = false;
    }
}
