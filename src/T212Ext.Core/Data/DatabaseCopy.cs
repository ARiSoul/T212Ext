using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Arisoul.T212Ext.Core.Data;

/// <summary>A table's rows in the source and in the target after a copy.</summary>
public sealed record TableCount(string Table, long Source, long Target)
{
    public bool Same => Source == Target;
}

/// <summary>
/// Copies every row of one T212Ext database into an empty PostgreSQL one, through the EF model, so
/// types convert as the app writes them (instants to UTC). Made for the move from SQL Server
/// (the <c>copy-from-sqlserver</c> command, 3.0.0), but the source can be any provider.
/// </summary>
/// <remarks>
/// <para>Tables go in foreign-key order, in one transaction: a failure leaves the target empty.
/// The target is migrated first and must have no rows. Owned rows (the positions of a snapshot) go
/// with their owner.</para>
/// <para>Keys are copied as they are, so the identity sequences are moved past the highest copied
/// key afterwards; otherwise the next insert would reuse key 1 and fail.</para>
/// </remarks>
public static class DatabaseCopy
{
    private const int Batch = 500;

    public static async Task<IReadOnlyList<TableCount>> CopyAsync(T212ExtDbContext source, T212ExtDbContext target, Action<string>? log, CancellationToken cancellationToken = default)
    {
        await target.Database.MigrateAsync(cancellationToken);

        var types = InForeignKeyOrder(target.Model);
        foreach (var table in Tables(target.Model))
        {
            if (await CountAsync(target, table, cancellationToken) > 0)
                throw new InvalidOperationException($"The target database already has rows in {table}: it must be empty.");
        }

        await using (var transaction = await target.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var type in types)
            {
                var rows = await (Task<int>)CopyMethod.MakeGenericMethod(type.ClrType).Invoke(null, [source, target, cancellationToken])!;
                log?.Invoke($"{type.GetTableName()}: {rows}");
            }

            await ResetIdentitiesAsync(target, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await CompareAsync(source, target, cancellationToken);
    }

    /// <summary>Row counts of every table, owned ones included, in the source and the target.</summary>
    public static async Task<IReadOnlyList<TableCount>> CompareAsync(T212ExtDbContext source, T212ExtDbContext target, CancellationToken cancellationToken = default)
    {
        var counts = new List<TableCount>();
        foreach (var table in Tables(target.Model))
            counts.Add(new TableCount(table, await CountAsync(source, table, cancellationToken), await CountAsync(target, table, cancellationToken)));
        return counts;
    }

    /// <summary>Every table of the model, owned ones (positions of a snapshot, taxes of an order) after their owner's.</summary>
    internal static IReadOnlyList<string> Tables(IModel model) =>
        [.. InForeignKeyOrder(model).SelectMany(t => new[] { t.GetTableName()! }
            .Concat(t.GetNavigations().Where(n => n.TargetEntityType.IsOwned()).Select(n => n.TargetEntityType.GetTableName()!)))
            .Distinct()];

    /// <summary>The entity types that have a table of their own, principals before dependents.</summary>
    internal static IReadOnlyList<IEntityType> InForeignKeyOrder(IModel model)
    {
        var pending = model.GetEntityTypes().Where(t => !t.IsOwned() && t.GetTableName() is not null).ToList();
        var ordered = new List<IEntityType>();
        while (pending.Count > 0)
        {
            var ready = pending.Where(t => t.GetForeignKeys().All(fk =>
                fk.PrincipalEntityType == t || !pending.Contains(fk.PrincipalEntityType))).OrderBy(t => t.GetTableName()).ToList();
            if (ready.Count == 0)
                throw new InvalidOperationException("The foreign keys form a cycle: " + string.Join(", ", pending.Select(t => t.GetTableName())));
            ordered.AddRange(ready);
            pending.RemoveAll(ready.Contains);
        }
        return ordered;
    }

    private static readonly MethodInfo CopyMethod = typeof(DatabaseCopy).GetMethod(nameof(CopyTableAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static async Task<int> CopyTableAsync<T>(T212ExtDbContext source, T212ExtDbContext target, CancellationToken cancellationToken) where T : class
    {
        var rows = await source.Set<T>().AsNoTracking().ToListAsync(cancellationToken);
        foreach (var chunk in rows.Chunk(Batch))
        {
            target.Set<T>().AddRange(chunk);
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();
        }
        return rows.Count;
    }

    // The same SQL on SQL Server and PostgreSQL: quoted names, and COUNT cast to bigint (SQL Server's is an int).
    // Table names come from the model, not from outside.
    private static async Task<long> CountAsync(T212ExtDbContext db, string table, CancellationToken cancellationToken)
    {
#pragma warning disable EF1002 // A table name cannot be a parameter; it comes from the model.
        var rows = await db.Database.SqlQueryRaw<long>($"SELECT CAST(COUNT(*) AS bigint) AS \"Value\" FROM \"{table}\"").ToListAsync(cancellationToken);
#pragma warning restore EF1002
        return rows.Single();
    }

    /// <summary>Every identity column's sequence moves past the highest key in its table (PostgreSQL).</summary>
    private static async Task ResetIdentitiesAsync(T212ExtDbContext db, CancellationToken cancellationToken)
    {
        var columns = await db.Database
            .SqlQueryRaw<IdentityColumn>("SELECT table_name AS \"Table\", column_name AS \"Column\" FROM information_schema.columns WHERE table_schema = current_schema() AND is_identity = 'YES'")
            .ToListAsync(cancellationToken);

        foreach (var (table, column) in columns.Select(c => (c.Table, c.Column)))
        {
            // Names come from the catalogue, quoted as identifiers; nothing here comes from outside.
            var sql = $"SELECT setval(pg_get_serial_sequence('\"{table}\"', '{column}'), COALESCE(MAX(\"{column}\"), 1), MAX(\"{column}\") IS NOT NULL) FROM \"{table}\"";
            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    private sealed class IdentityColumn
    {
        public string Table { get; init; } = "";
        public string Column { get; init; } = "";
    }
}
