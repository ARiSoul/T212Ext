using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Arisoul.T212Ext.Core.Tests.Infrastructure;

/// <summary>
/// One throwaway PostgreSQL database for the whole test run, created with the real migrations.
/// Server: T212EXT_TEST_PG (a connection string; its database is replaced), else a postgres container
/// started for the run (Docker needed), with the same image and locale as production.
/// Tests stay apart by creating their own account (<see cref="CreateAccountAsync"/>).
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime, IDbContextFactory<T212ExtDbContext>
{
    /// <summary>The image and initdb arguments of docker-compose.yml's t212ext-db.</summary>
    public const string Image = "postgres:18";

    public const string InitDbArgs = "--locale-provider=builtin --builtin-locale=C.UTF-8";

    private PostgreSqlContainer? _container;
    private string _server = "";
    private DbContextOptions<T212ExtDbContext> _options = null!;

    public T212ExtDbContext CreateDbContext() => new(_options);

    /// <summary>Options for another database on the same server (created by the first migration).</summary>
    public DbContextOptions<T212ExtDbContext> OptionsFor(string database) =>
        new DbContextOptionsBuilder<T212ExtDbContext>()
            .UseNpgsql(new NpgsqlConnectionStringBuilder(_server) { Database = database }.ConnectionString)
            .Options;

    public async Task InitializeAsync()
    {
        _server = Environment.GetEnvironmentVariable("T212EXT_TEST_PG") ?? "";
        if (string.IsNullOrEmpty(_server))
        {
            _container = new PostgreSqlBuilder(Image)
                .WithEnvironment("POSTGRES_INITDB_ARGS", InitDbArgs)
                .Build();
            await _container.StartAsync();
            _server = _container.GetConnectionString();
        }

        _options = OptionsFor($"t212ext_tests_{Guid.NewGuid():N}");
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var db = CreateDbContext())
            await db.Database.EnsureDeletedAsync();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public async Task<BrokerAccount> CreateAccountAsync(string? currency = "EUR")
    {
        await using var db = CreateDbContext();
        var account = new BrokerAccount
        {
            Key = $"test-{Guid.NewGuid():N}"[..30],
            Name = "Test account",
            Environment = "Demo",
            Currency = currency,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
