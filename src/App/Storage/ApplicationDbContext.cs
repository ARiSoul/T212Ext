using Arisoul.T212.Models;
using SQLite;
using System.Linq.Expressions;

namespace Arisoul.T212.App.Storage;

internal class ApplicationDbContext
{
    SQLiteAsyncConnection _database;

    async Task Init()
    {
        if (_database is not null)
            return;

        _database = new SQLiteAsyncConnection(Constants.DatabasePath, Constants.Flags);
        await _database.CreateTableAsync<Instrument>();
        await _database.CreateTableAsync<HistoryDividendItem>();
        await _database.CreateTableAsync<HistoryOrder>();
        await _database.CreateTableAsync<Tax>();
        await _database.CreateTableAsync<Parameter>();
    }

    public async Task<IEnumerable<Instrument>> GetInstrumentsAsync()
    {
        await Init();
        return await _database.Table<Instrument>().ToListAsync();
    }

    public async Task<int> SaveInstrumentsAsync(IEnumerable<Instrument> instruments)
    {
        await Init();
        return await _database.InsertAllAsync(instruments);
    }

    public async Task<IEnumerable<HistoryDividendItem>> GetHistoryDividendItemsAsync()
    {
        await Init();
        return await _database.Table<HistoryDividendItem>().ToListAsync();
    }

    public async Task<IEnumerable<DividendModel>> GetDividendsAsync()
    {
        await Init();

        var historyDividendItems = await _database.Table<HistoryDividendItem>().ToListAsync();
        var dividendTickers = historyDividendItems.Select(d => d.Ticker).Distinct();
        var instruments = await _database.Table<Instrument>().Where(i => dividendTickers.Contains(i.Ticker)).ToListAsync();

        return historyDividendItems.Select(dividend =>
        {
            var dividendModel = new DividendModel
            {
                Amount = dividend.Amount,
                AmountInEuro = dividend.AmountInEuro,
                GrossAmountPerShare = dividend.GrossAmountPerShare,
                PaidOn = dividend.PaidOn,
                Quantity = dividend.Quantity,
                Reference = dividend.Reference,
                Type = dividend.Type,
                Ticker = dividend.Ticker,
                Instrument = instruments.FirstOrDefault(i => i.Ticker == dividend.Ticker)!
            };

            return dividendModel;
        });
    }

    public async Task<int> SaveHistoryDividendItemsAsync(IEnumerable<HistoryDividendItem> historyDividendItems)
    {
        await Init();
        return await _database.InsertAllAsync(historyDividendItems);
    }

    public async Task<IEnumerable<HistoryOrderModel>> GetHistoryOrdersAsync(Expression<Func<HistoryOrder, bool>>? predicate = null)
    {
        await Init();

        List<HistoryOrder> orders = [];

        if (predicate is not null)
            orders = await _database.Table<HistoryOrder>().Where(predicate).ToListAsync();
        else
            orders = await _database.Table<HistoryOrder>().ToListAsync();

        var orderIds = orders.Select(o => o.Id);
        var taxes = await _database.Table<Tax>().Where(t => orderIds.Contains(t.OrderId)).ToListAsync();

        return orders.Select(order =>
        {
            var historyOrder = new HistoryOrderModel
            {
                Id = order.Id,
                DateCreated = order.DateCreated,
                DateExecuted = order.DateExecuted,
                DateModified = order.DateModified,
                Executor = order.Executor,
                FillCost = order.FillCost,
                FillId = order.FillId,
                FillPrice = order.FillPrice,
                FillResult = order.FillResult,
                FilledQuantity = order.FilledQuantity,
                FilledValue = order.FilledValue,
                FillType = order.FillType,
                LimitPrice = order.LimitPrice,
                OrderedQuantity = order.OrderedQuantity,
                OrderedValue = order.OrderedValue,
                ParentOrder = order.ParentOrder,
                Status = order.Status,
                StopPrice = order.StopPrice,
                Ticker = order.Ticker,
                Type = order.Type,
                Taxes = taxes.Where(t => t.OrderId == order.Id).ToList()
            };

            return historyOrder;
        });
    }

    public async Task<int> SaveHistoryOrdersAsync(IEnumerable<HistoryOrderModel> historyOrders)
    {
        await Init();

        // grant tax order id
        foreach (var historyOrder in historyOrders)
            foreach (var tax in historyOrder.Taxes)
                tax.OrderId = historyOrder.Id!.Value;

        var orders = historyOrders.Select(order =>
        {
            var historyOrder = new HistoryOrder
            {
                Id = order.Id,
                DateCreated = order.DateCreated,
                DateExecuted = order.DateExecuted,
                DateModified = order.DateModified,
                Executor = order.Executor,
                FillCost = order.FillCost,
                FillId = order.FillId,
                FillPrice = order.FillPrice,
                FillResult = order.FillResult,
                FilledQuantity = order.FilledQuantity,
                FilledValue = order.FilledValue,
                FillType = order.FillType,
                LimitPrice = order.LimitPrice,
                OrderedQuantity = order.OrderedQuantity,
                OrderedValue = order.OrderedValue,
                ParentOrder = order.ParentOrder,
                Status = order.Status,
                StopPrice = order.StopPrice,
                Ticker = order.Ticker,
                Type = order.Type
            };

            return historyOrder;
        });

        var taxes = historyOrders.SelectMany(order => order.Taxes);

        var insertedOrders = await _database.InsertAllAsync(orders);
        var insertedTaxes = await _database.InsertAllAsync(taxes);

        return insertedOrders + insertedTaxes;
    }

    public async Task<string?> GetParameterValueAsync(string key)
    {
        await Init();
        var parameter = await _database.Table<Parameter>().Where(p => p.Key == key).FirstOrDefaultAsync();

        return parameter?.Value;
    }

    public async Task<int> SaveParameterValueAsync(string key, string value)
    {
        await Init();

        var parameter = await _database.Table<Parameter>().Where(p => p.Key == key).FirstOrDefaultAsync();

        if (parameter is null)
        {
            parameter = new Parameter
            {
                Key = key,
                Value = value
            };

            return await _database.InsertAsync(parameter);
        }

        parameter.Value = value;

        return await _database.UpdateAsync(parameter);
    }
}
