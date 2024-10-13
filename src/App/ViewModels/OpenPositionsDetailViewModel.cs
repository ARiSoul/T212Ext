using Arisoul.T212.App.Storage;
using Arisoul.T212.Client.Services;
using Arisoul.T212.Models;

namespace Arisoul.T212.App.ViewModels;

[QueryProperty(nameof(Item), "Item")]
public partial class OpenPositionsDetailViewModel : BaseViewModel
{
    private readonly HistoryRestService _historyRestService;
    private readonly ApplicationDbContext _dbContext;

    [ObservableProperty] PositionModel? _item;
    [ObservableProperty] decimal _totalInvested = 0;

    public OpenPositionsDetailViewModel(IServiceProvider serviceProvider)
    {
        _historyRestService = serviceProvider.GetRequiredService<HistoryRestService>();
        _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
    }

    [RelayCommand]
    private async Task LoadTickerOrdersAsync()
    {
        try
        {
            if (Item is null)
                return;

            if (IsRefreshing)
                return;

            IsRefreshing = true;

            Debug.WriteLine("Syncing ticker orders...");

            List<HistoryOrderModel> ordersInDatabase = (await _dbContext.GetHistoryOrdersAsync(x => x.Ticker == Item.Ticker)).ToList();

            string? lastOrderCursor = null;

            do
            {
                lastOrderCursor = await GetOrdersRecursivelyAsync(lastOrderCursor, ordersInDatabase);
            } while (lastOrderCursor is not null);

            // update position orders
            Debug.WriteLine("Updating position orders...");
            var orders = ordersInDatabase.Where(i => i.Ticker == Item.Ticker);
            Item.Orders = orders.ToList();

            TotalInvested = Item.Orders.Sum(x => x.FilledValue!) ?? 0;

            Debug.WriteLine("Finished syncing position orders.");
        }
        catch (Exception ex)
        {
            await Dialogs.ShowError(ex.Message);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task<string?> GetOrdersRecursivelyAsync(string? nextCursor, List<HistoryOrderModel> ordersInDatabase)
    {
        if (Item is null)
            return null;

        CursorLimitTickerRequestParams requestParams = new()
        {
            Ticker = Item.Ticker
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
