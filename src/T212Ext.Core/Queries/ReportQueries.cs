using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Queries;

public sealed record ReportRow(
    long Id,
    string AccountName,
    DateTimeOffset From,
    DateTimeOffset To,
    AccountReportStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? FinishedAt,
    int DividendsAdded,
    int DividendsMatched,
    int TransactionsAdded,
    int TransactionsMatched,
    string? Error);

/// <summary>The CSV reports asked for through the API, for the import page.</summary>
public sealed class ReportQueries(IDbContextFactory<T212ExtDbContext> dbFactory)
{
    public async Task<IReadOnlyList<ReportRow>> GetReportsAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // Ordered before the projection: EF cannot order by a member of the record.
        return await (
            from r in db.AccountReports.AsNoTracking()
            join a in db.Accounts on r.AccountId equals a.Id
            where accountId == null || r.AccountId == accountId
            orderby r.RequestedAt descending, r.Id descending
            select new ReportRow(
                r.Id, a.Name, r.From, r.To, r.Status, r.RequestedAt, r.FinishedAt,
                r.DividendsAdded, r.DividendsMatched, r.TransactionsAdded, r.TransactionsMatched, r.Error))
            .ToListAsync(cancellationToken);
    }
}
