using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Tests.Infrastructure;

/// <summary>
/// One throwaway SQL Server database for the whole test run, created with the real migrations.
/// Server: T212EXT_TEST_SQL (a connection string without Database), LocalDB by default.
/// Tests stay apart by creating their own account (<see cref="CreateAccountAsync"/>).
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime, IDbContextFactory<T212ExtDbContext>
{
    private const string DefaultServer = @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    private readonly DbContextOptions<T212ExtDbContext> _options;

    public DatabaseFixture()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("T212EXT_TEST_SQL") ?? DefaultServer)
        {
            InitialCatalog = $"T212ExtTests_{Guid.NewGuid():N}",
        };
        _options = new DbContextOptionsBuilder<T212ExtDbContext>().UseSqlServer(builder.ConnectionString).Options;
    }

    public T212ExtDbContext CreateDbContext() => new(_options);

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
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
