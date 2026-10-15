using Arisoul.T212Ext.Client;
using Arisoul.T212Ext.Core.Accounts;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Import;
using Arisoul.T212Ext.Core.Logos;
using Arisoul.T212Ext.Core.Notifications;
using Arisoul.T212Ext.Core.Queries;
using Arisoul.T212Ext.Core.Rates;
using Arisoul.T212Ext.Core.Reports;
using Arisoul.T212Ext.Core.Sync;
using Arisoul.T212Ext.Core.Tax;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Arisoul.T212Ext.Core;

public static class CoreServiceCollectionExtensions
{
    public const string ConnectionStringName = "T212Ext";

    /// <summary>Everything except the background worker (<see cref="AddT212ExtSyncWorker"/>).</summary>
    public static IServiceCollection AddT212ExtCore(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContextFactory<T212ExtDbContext>(options => options.UseSqlServer(connectionString));

        services.AddOptions<T212Options>().Bind(configuration.GetSection(T212Options.Section));
        services.AddOptions<SyncOptions>().Bind(configuration.GetSection(SyncOptions.Section));
        services.AddOptions<ReportOptions>().Bind(configuration.GetSection(ReportOptions.Section));
        services.AddOptions<PushOptions>().Bind(configuration.GetSection(PushOptions.Section));
        services.AddOptions<LogoOptions>().Bind(configuration.GetSection(LogoOptions.Section));
        services.AddSingleton<AccountRegistry>();

        services.AddT212Client();
        services.AddSingleton<SyncCoordinator>();
        services.AddScoped<SyncRunner>();
        services.AddScoped<AccountSynchronizer>();
        services.AddScoped<CsvImportService>();
        services.AddScoped<ReportImporter>();
        services.AddScoped<ReportQueries>();
        services.AddScoped<PushDevices>();
        services.AddSingleton<InstrumentLogos>();
        services.AddHttpClient(InstrumentLogos.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("T212Ext (personal dividend tracker)");
        });
        services.AddScoped<Notifier>();
        services.AddHttpClient(WebPushSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        services.TryAddSingleton<IPushSender, WebPushSender>();
        services.AddScoped<DividendQueries>();
        services.AddScoped<OverviewQueries>();
        services.AddScoped<AnalyticsQueries>();
        services.AddScoped<LedgerQueries>();
        services.AddScoped<InstrumentSettings>();
        services.AddScoped<TaxQueries>();

        services.AddHttpClient(EcbRateSource.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("T212Ext (personal dividend tracker)");
        });
        services.TryAddSingleton<IExchangeRateSource, EcbRateSource>();
        services.AddScoped<ExchangeRateUpdater>();

        return services;
    }

    public static IServiceCollection AddT212ExtSyncWorker(this IServiceCollection services) =>
        services.AddHostedService<SyncWorker>();

    /// <summary>
    /// Applies pending migrations and creates the rows of the configured accounts. Run once at startup.
    /// </summary>
    /// <remarks>
    /// Retries while SQL Server cannot be reached: LocalDB can fail to start when two processes
    /// wake it at once, and a SQL Server container may still be starting.
    /// </remarks>
    public static async Task InitializeT212ExtAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<T212ExtDbContext>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CoreServiceCollectionExtensions));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var db = await factory.CreateDbContextAsync(cancellationToken);
                await db.Database.MigrateAsync(cancellationToken);
                await scope.ServiceProvider.GetRequiredService<AccountRegistry>().EnsureAccountsAsync(db, cancellationToken);
                return;
            }
            catch (SqlException ex) when (attempt < StartupAttempts)
            {
                var wait = TimeSpan.FromSeconds(5 * attempt);
                logger.LogWarning("SQL Server not reachable at startup ({Error}); attempt {Attempt} of {Max}, retrying in {Wait}",
                    ex.Message, attempt, StartupAttempts, wait);
                await Task.Delay(wait, cancellationToken);
            }
        }
    }

    private const int StartupAttempts = 5;
}
