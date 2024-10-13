using Arisoul.T212.Client.Services;
using Arisoul.T212.Client;
using Arisoul.T212.Models;
using Arisoul.T212.App.Storage;

namespace Arisoul.T212.App.ViewModels;

public partial class OpenPositionsViewModel
    : BaseViewModel
{
    private readonly PortfolioRestService _portfolioRestService;
    private readonly InstrumentRestService _instrumentRestService;
    private readonly HistoryRestService _historyRestService;
    private readonly IT212ClientOptions _clientOptions;
    private readonly ApplicationDbContext _dbContext;
    private readonly BackgroundSyncService _backgroundSyncService;

    [ObservableProperty] ObservableCollection<PositionModel>? _items;
    [ObservableProperty] decimal _totalInvested = 0;
    [ObservableProperty] decimal _totalReturn = 0;
    [ObservableProperty] decimal _totalDividends = 0;
    [ObservableProperty] decimal _totalDividendsOthers = 0;

    public OpenPositionsViewModel(IServiceProvider serviceProvider)
    {
        _portfolioRestService = serviceProvider.GetRequiredService<PortfolioRestService>();
        _instrumentRestService = serviceProvider.GetRequiredService<InstrumentRestService>();
        _historyRestService = serviceProvider.GetRequiredService<HistoryRestService>();
        _clientOptions = serviceProvider.GetRequiredService<IT212ClientOptions>();
        _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        _backgroundSyncService = serviceProvider.GetRequiredService<BackgroundSyncService>();
        _clientOptions.LoadOptions();
    }

    [RelayCommand]
    private async Task OnRefreshing()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        await LoadPositionsAsync();
    }

    [RelayCommand]
    public async Task LoadPositionsAsync()
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

            var result = await _portfolioRestService.GetOpenPositionsAsync();

            if (result.Success)
            {
                await SyncTickerNamesAsync(result.Value!);
                await GetPositionDividendsAsync(result.Value!);
                await GetTickerOrdersAsync(result.Value!);
                Items = new ObservableCollection<PositionModel>(result.Value!.OrderBy(x => x.Ticker)!);
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
    private async Task GoToDetails(PositionModel item)
    {
        await Shell.Current.GoToAsync(nameof(OpenPositionsDetailPage), true, new Dictionary<string, object>
        {
            { "Item", item }
        });
    }

    private async Task SyncTickerNamesAsync(IEnumerable<PositionModel> positions)
    {
        Debug.WriteLine("Syncing ticker names...");
        var instruments = await _dbContext.GetInstrumentsAsync();

        // check if all position tickers exist in instruments
        var missingTickers = positions!.Cast<PositionModel>().Where(i => instruments.All(x => x.Ticker != i.Ticker)).ToList();

        // if there are missing tickers, fetch them from instruments service
        if (missingTickers.Count != 0 || !instruments.Any())
        {
            Debug.WriteLine("Fetching missing tickers...");
            var result = await _instrumentRestService.GetInstrumentsAsync();
            if (result.Success)
            {
                Debug.WriteLine("Saving instruments...");
                await _dbContext.SaveInstrumentsAsync(result.Value!);
                instruments = result.Value!;
            }
            else
            {
                await Dialogs.ShowError(result.Message);
                return;
            }
        }

        // update position names
        Debug.WriteLine("Updating position names...");
        foreach (PositionModel item in positions!.Cast<PositionModel>())
        {
            var instrument = instruments.FirstOrDefault(i => i.Ticker == item.Ticker);
            if (instrument is not null)
                item.Instrument = instrument;
            else
                Debug.WriteLine($"Ticker {item.Ticker} not found???");
        }

        Debug.WriteLine("Finished syncing position names.");
    }

    private async Task GetPositionDividendsAsync(IEnumerable<PositionModel> positions)
    {
        await _backgroundSyncService.SyncDividendsAsync().ConfigureAwait(false);

        Debug.WriteLine("Getting ticker dividends...");
        List<HistoryDividendItem> dividendsInDatabase = (await _dbContext.GetHistoryDividendItemsAsync()).ToList();

        // update position dividends
        Debug.WriteLine("Updating position dividends...");
        foreach (PositionModel item in positions!.Cast<PositionModel>())
        {
            var dividends = dividendsInDatabase.Where(i => i.Ticker == item.Ticker);
            item.Dividends = dividends.ToList();
        }

        // now, there can be dividends of instruments that are not in positions, so we need to add them
        var missingDividends = dividendsInDatabase.Where(i => positions.All(x => x.Ticker != i.Ticker)).ToList();

        TotalDividendsOthers = missingDividends.Sum(x => x.AmountInEuro) ?? 0;
        TotalDividends = (positions.Sum(x => x.TotalDividendsInEuro!) ?? 0) + TotalDividendsOthers;
        TotalReturn = positions.Sum(x => x.Ppl!) ?? 0;
        TotalInvested = positions.Sum(x => x.Invested!) ?? 0;

        Debug.WriteLine("Finished getting position dividends.");
    }

    private async Task GetTickerOrdersAsync(IEnumerable<PositionModel> positions)
    {
        Debug.WriteLine("Getting ticker orders...");

        List<HistoryOrderModel> ordersInDatabase = (await _dbContext.GetHistoryOrdersAsync()).ToList();

        // update position orders
        Debug.WriteLine("Updating position orders...");
        foreach (PositionModel item in positions!.Cast<PositionModel>())
        {
            var orders = ordersInDatabase.Where(i => i.Ticker == item.Ticker);
            item.Orders = orders.ToList();
        }

        TotalInvested = positions.Sum(x => x.Invested!) ?? 0;

        Debug.WriteLine("Finished getting position orders.");
    }
}
