using Arisoul.T212.App.Storage;
using Arisoul.T212.Client;
using Arisoul.T212.Client.Services;
using Arisoul.T212.Models;

namespace Arisoul.T212.App.Services;

public class BackgroundSyncService
{
    private readonly HistoryRestService _historyRestService;
    private readonly IT212ClientOptions _clientOptions;
    private readonly ApplicationDbContext _dbContext;

    private const int _dividendsRequestRateDelay = 60000;
    private const int _ordersRequestRateDelay = 60000;

    public BackgroundSyncService(IServiceProvider serviceProvider)
    {
        _historyRestService = serviceProvider.GetRequiredService<HistoryRestService>();
        _clientOptions = serviceProvider.GetRequiredService<IT212ClientOptions>();
        _dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        _clientOptions.LoadOptions();
    }

    public async Task SyncDividendsAsync()
    {
        if (!_clientOptions.IsInitiated)
            return;

        Debug.WriteLine("Syncing ticker dividends...");
        List<HistoryDividendItem> dividendsInDatabase = (await _dbContext.GetHistoryDividendItemsAsync().ConfigureAwait(false)).ToList();

        await GetDividendsRecursivelyAsync(null, dividendsInDatabase).ConfigureAwait(false);

        Debug.WriteLine("Finished syncing position dividends.");
    }

    public async Task SyncOrdersAsync()
    {
        if (!_clientOptions.IsInitiated)
            return;

        Debug.WriteLine("Syncing ticker orders...");

        List<HistoryOrderModel> ordersInDatabase = (await _dbContext.GetHistoryOrdersAsync().ConfigureAwait(false)).ToList();

        await GetOrdersRecursivelyAsync(null, ordersInDatabase).ConfigureAwait(false);

        Debug.WriteLine("Finished syncing position orders.");
    }

    private async Task<string?> GetDividendsRecursivelyAsync(string? nextCursor, List<HistoryDividendItem> dividendsInDatabase)
    {
        CursorLimitTickerRequestParams requestParams = new()
        {
            Cursor = nextCursor
        };

        var result = await _historyRestService.GetDividendsAsync(requestParams).ConfigureAwait(false);

        if (result.Success)
        {
            // get dividends obtained that don't exist in database
            var newDividends = result.Value!.Items!.Where(i => dividendsInDatabase.All(x => x.Reference != i.Reference)).ToList();

            // save new dividends if any
            if (newDividends.Count != 0)
            {
                Debug.WriteLine("Saving new dividends...");
                await _dbContext.SaveHistoryDividendItemsAsync(newDividends).ConfigureAwait(false);
                dividendsInDatabase.AddRange(newDividends);

                // since this comes from the most recent to the older ones, if the count of newDividends is different from the return value
                // it means that everything else is already sync, so we can leave
                if (newDividends.Count != result.Value!.Items!.Count())
                    return null;
            }
            else
                return null;

            // if next page path is not null, extract cursor from it and save it
            if (result.Value!.NextPagePath is not null)
            {
                var fromNextCursorPart = result.Value!.NextPagePath.Split("cursor=")[1];
                var nextCursor2 = fromNextCursorPart.Split("&")[0];

                while (nextCursor2 is not null)
                    nextCursor2 = await GetDividendsRecursivelyAsync(nextCursor2, dividendsInDatabase).ConfigureAwait(false);
            }
        }
        else if (!string.IsNullOrEmpty(result.Message) && result.Message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase))
        {
            await Task.Delay(_dividendsRequestRateDelay).ConfigureAwait(false);
            await GetDividendsRecursivelyAsync(nextCursor, dividendsInDatabase).ConfigureAwait(false);
        }
        else
        {
            Debug.WriteLine($"Sync dividends failed: {result.Message}");
        }

        return null;
    }

    private async Task<string?> GetOrdersRecursivelyAsync(string? nextCursor, List<HistoryOrderModel> ordersInDatabase)
    {
        CursorLimitTickerRequestParams requestParams = new()
        {
            Cursor = nextCursor
        };

        var result = await _historyRestService.GetOrdersAsync(requestParams).ConfigureAwait(false);

        if (result.Success && result.Value is not null && result.Value.Items.Any())
        {
            var alphaOrders = result.Value!.Items!.Where(i => i.Ticker == "ABECd_EQ").ToList();

            // for debug, print all alpha orders
            foreach (var alphaOrder in alphaOrders)
                Debug.WriteLine($"Alpha order: {alphaOrder.Id}");

            // get orders obtained that don't exist in database
            var newOrders = result.Value!.Items!.Where(i => ordersInDatabase.All(x => x.Id != i.Id)).ToList();
            Debug.WriteLine($"New orders count: {newOrders.Count}");

            // save new orders if any
            if (newOrders.Count != 0)
            {
                Debug.WriteLine("Saving new orders...");
                await _dbContext.SaveHistoryOrdersAsync(newOrders).ConfigureAwait(false);
                ordersInDatabase.AddRange(newOrders);
            }

            // Get the date created of the last order in the result
            var lastResultOrder = result.Value!.Items.LastOrDefault();

            var minDateCreated = lastResultOrder!.DateCreated!.Value;
            Debug.WriteLine($"Id of last order in result: {lastResultOrder.Id}");
            Debug.WriteLine($"Date created from last order in result: {minDateCreated}");

            // TODO: they need to fix the API to return the correct cursor
            // Get the timestamp of the min date created
            var minDateCreatedTimestamp = new DateTimeOffset(minDateCreated).ToUnixTimeMilliseconds();
            Debug.WriteLine($"Min date created timestamp: {minDateCreatedTimestamp}");

            // Use the timestamp as cursor to fetch the next orders
            var nextCursor2 = minDateCreatedTimestamp.ToString();
            while (nextCursor2 is not null)
                nextCursor2 = await GetOrdersRecursivelyAsync(nextCursor2, ordersInDatabase).ConfigureAwait(false);
        }
        else if (!string.IsNullOrEmpty(result.Message) && result.Message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase))
        {
            Debug.WriteLine("Too many requests, delaying...");
            await Task.Delay(_ordersRequestRateDelay).ConfigureAwait(false);
            await GetOrdersRecursivelyAsync(nextCursor, ordersInDatabase).ConfigureAwait(false);
        }
        else if (result.Failure)
        {
            Debug.WriteLine($"Sync orders failed: {result.Message}");
        }

        return null;
    }
}
