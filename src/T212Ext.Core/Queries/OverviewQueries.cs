using Arisoul.T212Ext.Core.Accounts;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Queries;

public sealed record AccountOverview(
    int Id,
    string Key,
    string Name,
    string Environment,
    string? Currency,
    bool HasCredentials,
    PortfolioSnapshot? LatestSnapshot,
    int DividendCount,
    int OrderCount,
    int TransactionCount,
    long? ExternalId);

public sealed record SyncRunRow(
    long Id,
    string? AccountName,
    SyncJob Job,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    bool Succeeded,
    int ItemsAdded,
    int ItemsUpdated,
    string? Error);

public sealed class OverviewQueries(IDbContextFactory<T212ExtDbContext> dbFactory, AccountRegistry registry)
{
    public async Task<IReadOnlyList<AccountOverview>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var accounts = await db.Accounts.AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var result = new List<AccountOverview>();

        foreach (var account in accounts)
        {
            var snapshot = await db.Snapshots.AsNoTracking().Include(s => s.Positions)
                .Where(s => s.AccountId == account.Id)
                .OrderByDescending(s => s.Date)
                .FirstOrDefaultAsync(cancellationToken);

            result.Add(new AccountOverview(
                account.Id,
                account.Key,
                account.Name,
                account.Environment,
                account.Currency,
                registry.Find(account.Key)?.ToT212Account().HasCredentials ?? false,
                snapshot,
                await db.Dividends.CountAsync(d => d.AccountId == account.Id, cancellationToken),
                await db.Orders.CountAsync(o => o.AccountId == account.Id, cancellationToken),
                await db.CashTransactions.CountAsync(t => t.AccountId == account.Id, cancellationToken),
                account.ExternalId));
        }

        return result;
    }

    public async Task<IReadOnlyList<SyncRunRow>> GetRecentRunsAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SyncRuns.AsNoTracking()
            .OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
            .Take(count)
            .Select(r => new SyncRunRow(r.Id, r.Account != null ? r.Account.Name : null, r.Job, r.StartedAt, r.FinishedAt,
                r.Succeeded, r.ItemsAdded, r.ItemsUpdated, r.Error))
            .ToListAsync(cancellationToken);
    }
}
