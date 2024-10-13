using Arisoul.Core.Static;
using Arisoul.T212.App.Storage;
using Arisoul.T212.Client;
using Arisoul.T212.Client.Services;
using Arisoul.T212.Models;

namespace Arisoul.T212.App.ViewModels;

public partial class DividendsViewModel : BaseViewModel
{
    private readonly HistoryRestService _historyRestService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IT212ClientOptions _clientOptions;
    private readonly BackgroundSyncService _backgroundSyncService;

    [ObservableProperty] ObservableCollection<MonthlyDividendGroup> _items;
    [ObservableProperty] decimal _totalDividends = 0;
    [ObservableProperty] decimal _totalDividendsOthers = 0;
    [ObservableProperty] decimal _totalDividendsCount = 0;

    public DividendsViewModel(IServiceProvider serviceProvider)
    {
        _historyRestService = serviceProvider.GetRequiredService<HistoryRestService>();
        _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        _backgroundSyncService = serviceProvider.GetRequiredService<BackgroundSyncService>();
        _clientOptions = serviceProvider.GetRequiredService<IT212ClientOptions>();
        _clientOptions.LoadOptions();
        Items = [];
    }

    [RelayCommand]
    private async Task OnRefreshing()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        await LoadDividendsAsync();
    }

    [RelayCommand]
    private async Task LoadDividendsAsync()
    {
        if (!_clientOptions.IsInitiated)
        {
            await Dialogs.ShowMessage("Please configure the app first.");
            await Shell.Current.GoToAsync("//SettingsPage");
            return;
        }

        if (Items is not null && Items.Any() && !IsRefreshing)
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

            await _backgroundSyncService.SyncDividendsAsync().ConfigureAwait(false);

            var dividends = await _dbContext.GetDividendsAsync();

            // TODO: group by year and month
            Items!.Clear();

            // Group by Year and Month
            var grouped = dividends
                .GroupBy(d => d.PaidOn!.Value.ToString("yyyy MMMM")) // Group by year and month
                .Select(g => new MonthlyDividendGroup(g.Key, g.ToList()))
                .ToList();

            Items = new ObservableCollection<MonthlyDividendGroup>(grouped);

            TotalDividends = grouped.Sum(x => x.MonthTotalPaid);
            TotalDividendsCount = dividends.Count();
        }
        catch (Exception ex)
        {
            HasError = true;
            await Dialogs.ShowError(ex.Message);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    Task ToggleGroupExpansion(MonthlyDividendGroup group)
    {
        // TODO: improve expansion logic
        group.IsExpanded = !group.IsExpanded;

        foreach (var item in group)
            item.IsVisible = group.IsExpanded;

        return Task.CompletedTask;
    }
}
