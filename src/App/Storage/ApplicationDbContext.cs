using Arisoul.T212.Models;
using SQLite;

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
    }

    public async Task<IEnumerable<Instrument>> GetInstrumentsAsync()
    {
        await Init();
        return await _database.Table<Instrument>().ToListAsync();
    }

    public async Task<Instrument> GetInstrumentAsync(string ticker)
    {
        await Init();
        return await _database.Table<Instrument>().Where(i => i.Ticker == ticker).FirstOrDefaultAsync();
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

    public async Task<IEnumerable<HistoryDividendItem>> GetHistoryDividendItemsAsync(string ticker)
    {
        await Init();
        return await _database.Table<HistoryDividendItem>().Where(i => i.Ticker == ticker).ToListAsync();
    }

    public async Task<int> SaveHistoryDividendItemsAsync(IEnumerable<HistoryDividendItem> historyDividendItems)
    {
        await Init();
        return await _database.InsertAllAsync(historyDividendItems);
    }
}
