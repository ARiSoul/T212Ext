using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Data;

public class T212ExtDbContext(DbContextOptions<T212ExtDbContext> options) : DbContext(options)
{
    public DbSet<BrokerAccount> Accounts => Set<BrokerAccount>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Dividend> Dividends => Set<Dividend>();
    public DbSet<HistoricalOrder> Orders => Set<HistoricalOrder>();
    public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();
    public DbSet<PortfolioSnapshot> Snapshots => Set<PortfolioSnapshot>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<InstrumentSetting> InstrumentSettings => Set<InstrumentSetting>();
    public DbSet<StockSplit> StockSplits => Set<StockSplit>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Fractional shares and FX rates need many decimals; amounts fit easily in 28,10.
        configurationBuilder.Properties<decimal>().HavePrecision(28, 10);
        configurationBuilder.Properties<decimal?>().HavePrecision(28, 10);
        configurationBuilder.Properties<SyncJob>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(T212ExtDbContext).Assembly);
}
