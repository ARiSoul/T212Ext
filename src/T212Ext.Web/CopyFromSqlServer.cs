using Arisoul.T212Ext.Core;
using Arisoul.T212Ext.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Web;

/// <summary>
/// The <c>copy-from-sqlserver</c> command (3.0.0): copies the whole database from SQL Server
/// (<c>ConnectionStrings:SqlServer</c>) into the empty PostgreSQL one (<c>ConnectionStrings:T212Ext</c>),
/// then compares the row count of every table. The source is only read. Run once, with the app
/// stopped (docs/UPGRADING.md, 3.0.0); it refuses a target that has rows.
/// </summary>
/// <remarks>Goes away, with the SQL Server package, once production has moved (backlog).</remarks>
public static class CopyFromSqlServer
{
    public const string Name = "copy-from-sqlserver";

    public static bool IsCommand(string[] args) => args is [Name, ..];

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error)
    {
        // The app's own configuration (appsettings, environment, .env through compose), as when it starts.
        var configuration = WebApplication.CreateBuilder(args[1..]).Configuration;
        var source = configuration.GetConnectionString("SqlServer");
        var target = configuration.GetConnectionString(CoreServiceCollectionExtensions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            error.WriteLine("Needs both ConnectionStrings:SqlServer (the old database, read only) and ConnectionStrings:T212Ext (the new PostgreSQL one).");
            return 2;
        }

        await using var from = new T212ExtDbContext(new DbContextOptionsBuilder<T212ExtDbContext>().UseSqlServer(source).Options);
        await using var to = new T212ExtDbContext(new DbContextOptionsBuilder<T212ExtDbContext>().UseNpgsql(target).Options);

        output.WriteLine("Copying from SQL Server to PostgreSQL...");
        IReadOnlyList<TableCount> counts;
        try
        {
            counts = await DatabaseCopy.CopyAsync(from, to, line => output.WriteLine($"  {line}"));
        }
        catch (InvalidOperationException ex)
        {
            error.WriteLine(ex.Message);
            return 1;
        }

        output.WriteLine();
        output.WriteLine($"{"table",-24} {"SQL Server",12} {"PostgreSQL",12}");
        foreach (var count in counts)
            output.WriteLine($"{count.Table,-24} {count.Source,12} {count.Target,12}{(count.Same ? "" : "   DIFFERENT")}");

        if (counts.All(c => c.Same))
        {
            output.WriteLine();
            output.WriteLine("Every table has the same number of rows. The SQL Server database was only read.");
            return 0;
        }

        error.WriteLine("Some tables differ: do not switch. The PostgreSQL database can be emptied and the copy run again (docs/UPGRADING.md).");
        return 1;
    }
}
