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

    [ObservableProperty] ObservableCollection<PositionModel>? _items;
    [ObservableProperty] decimal _totalDividends = 0;
    [ObservableProperty] decimal _totalInvested = 0;
    [ObservableProperty] decimal _totalReturn = 0;
    [ObservableProperty] decimal _totalDividendsOthers = 0;

    public OpenPositionsViewModel(IServiceProvider serviceProvider)
    {
        _portfolioRestService = serviceProvider.GetRequiredService<PortfolioRestService>();
        _instrumentRestService = serviceProvider.GetRequiredService<InstrumentRestService>();
        _historyRestService = serviceProvider.GetRequiredService<HistoryRestService>();
        _clientOptions = serviceProvider.GetRequiredService<IT212ClientOptions>();
        _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
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
                await SyncTickerDividendsAsync(result.Value!);
                // await SyncTickerOrdersAsync(result.Value!);
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

    private async Task SyncTickerDividendsAsync(IEnumerable<PositionModel> positions)
    {
        Debug.WriteLine("Syncing ticker dividends...");
        List<HistoryDividendItem> dividendsInDatabase = (await _dbContext.GetHistoryDividendItemsAsync()).ToList();

        string? lastDividendCursor = await _dbContext.GetParameterValueAsync(Storage.Constants.ParameterKeys.LastDividendCursor);

        do
        {
            lastDividendCursor = await GetDividendsRecursivelyAsync(lastDividendCursor, dividendsInDatabase);
        } while (lastDividendCursor is not null);

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

        Debug.WriteLine("Finished syncing position dividends.");
    }

    private async Task<string?> GetDividendsRecursivelyAsync(string? nextCursor, List<HistoryDividendItem> dividendsInDatabase)
    {
        CursorLimitTickerRequestParams requestParams = new()
        {
            Cursor = nextCursor
        };

        var result = await _historyRestService.GetDividendsAsync(requestParams);

        if (result.Success)
        {
            // get dividends obtained that don't exist in database
            var newDividends = result.Value!.Items!.Where(i => dividendsInDatabase.All(x => x.Reference != i.Reference)).ToList();

            // save new dividends if any
            if (newDividends.Count != 0)
            {
                Debug.WriteLine("Saving new dividends...");
                await _dbContext.SaveHistoryDividendItemsAsync(newDividends);
                dividendsInDatabase.AddRange(newDividends);
            }

            // if next page path is not null, extract cursor from it and save it
            if (result.Value!.NextPagePath is not null)
            {
                var fromNextCursorPart = result.Value!.NextPagePath.Split("cursor=")[1];
                var nextCursor2 = fromNextCursorPart.Split("&")[0];
                await _dbContext.SaveParameterValueAsync(Storage.Constants.ParameterKeys.LastDividendCursor, nextCursor2);

                while (nextCursor2 is not null)
                {
                    nextCursor2 = await GetDividendsRecursivelyAsync(nextCursor2, dividendsInDatabase);
                }
            }
        }
        else
        {
            await Dialogs.ShowError(result.Message);
        }

        return null;
    }

    private async Task SyncTickerOrdersAsync(IEnumerable<PositionModel> positions)
    {
        try
        {
            Debug.WriteLine("Syncing ticker orders...");

            List<HistoryOrderModel> ordersInDatabase = (await _dbContext.GetHistoryOrdersAsync()).ToList();

            string? lastOrderCursor = await _dbContext.GetParameterValueAsync(Storage.Constants.ParameterKeys.LastOrderCursor);

            do
            {
                lastOrderCursor = await GetOrdersRecursivelyAsync(lastOrderCursor, ordersInDatabase);
            } while (lastOrderCursor is not null);

            // update position orders
            Debug.WriteLine("Updating position orders...");
            foreach (PositionModel item in positions!.Cast<PositionModel>())
            {
                var orders = ordersInDatabase.Where(i => i.Ticker == item.Ticker);
                item.Orders = orders.ToList();
            }

            // now, there can be orders of instruments that are not in positions, so we need to add them
            var missingOrders = ordersInDatabase.Where(i => positions.All(x => x.Ticker != i.Ticker)).ToList();

            TotalInvested = positions.Sum(x => x.Invested!) ?? 0;

            Debug.WriteLine("Finished syncing position orders.");
        }
        catch (Exception ex)
        {

            throw;
        }
    }

    private async Task<string?> GetOrdersRecursivelyAsync(string? nextCursor, List<HistoryOrderModel> ordersInDatabase)
    {
        CursorLimitTickerRequestParams requestParams = new()
        {
            Cursor = nextCursor
        };

        var result = await _historyRestService.GetOrdersAsync(requestParams);

        if (result.Success)
        {
            // get orders obtained that don't exist in database
            var newOrders = result.Value!.Items!.Where(i => ordersInDatabase.All(x => x.Id != i.Id)).ToList();

            // save new orders if any
            if (newOrders.Count != 0)
            {
                Debug.WriteLine("Saving new orders...");
                await _dbContext.SaveHistoryOrdersAsync(newOrders);
                ordersInDatabase.AddRange(newOrders);
            }

            // if next page path is not null, extract cursor from it and save it
            if (result.Value!.NextPagePath is not null)
            {
                var fromNextCursorPart = result.Value!.NextPagePath.Split("cursor=")[1];
                var nextCursor2 = fromNextCursorPart.Split("&")[0];
                await _dbContext.SaveParameterValueAsync(Storage.Constants.ParameterKeys.LastOrderCursor, nextCursor2);

                while (nextCursor2 is not null)
                {
                    nextCursor2 = await GetOrdersRecursivelyAsync(nextCursor2, ordersInDatabase);
                }
            }
        }
        else
        {
            await Dialogs.ShowError(result.Message);
        }

        return null;
    }
}
