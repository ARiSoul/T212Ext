using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
    public DbSet<AccountReport> AccountReports => Set<AccountReport>();
    public DbSet<PushDevice> PushDevices => Set<PushDevice>();
    public DbSet<NotificationState> NotificationStates => Set<NotificationState>();
    public DbSet<InstrumentLogo> InstrumentLogos => Set<InstrumentLogo>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<SignInAttempt> SignInAttempts => Set<SignInAttempt>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Fractional shares and FX rates need many decimals; amounts fit easily in 28,10.
        configurationBuilder.Properties<decimal>().HavePrecision(28, 10);
        configurationBuilder.Properties<decimal?>().HavePrecision(28, 10);
        configurationBuilder.Properties<SyncJob>().HaveConversion<string>().HaveMaxLength(20);

        // timestamptz takes only UTC (offset 0): every instant is written, and compared, in UTC
        // whatever offset it came with (an API answer, a CSV file). See docs/POSTGRESQL.md.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcConverter>();
        configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<UtcConverter>();
    }

    private sealed class UtcConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(T212ExtDbContext).Assembly);
}
