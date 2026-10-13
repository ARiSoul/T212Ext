using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Queries;

public sealed record OrderRow(
    long Id,
    string AccountName,
    long OrderId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? FilledAt,
    string Ticker,
    string? InstrumentName,
    string? Isin,
    string? Side,
    string? Type,
    string? Status,
    string? InitiatedFrom,
    decimal? Quantity,
    decimal? Price,
    string? InstrumentCurrency,
    decimal? NetValue,
    string? WalletCurrency,
    decimal? FxRate,
    decimal? RealisedProfitLoss,
    decimal Fees);

public sealed record CashRow(
    long Id,
    string AccountName,
    DateTimeOffset DateTime,
    string Type,
    decimal Amount,
    string Currency,
    string Reference,
    DataSources Sources);

/// <summary>Read side of orders and cash movements for the grids. The grids filter, sort and page in memory.</summary>
public sealed class LedgerQueries(IDbContextFactory<T212ExtDbContext> dbFactory)
{
    public async Task<IReadOnlyList<OrderRow>> GetOrdersAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var orders = await db.Orders.AsNoTracking()
            .Where(o => accountId == null || o.AccountId == accountId)
            .Select(o => new
            {
                o.Id, Account = o.Account!.Name, o.OrderId, o.CreatedAt, o.FilledAt, o.Ticker, o.InstrumentName, o.Isin,
                o.Side, o.Type, o.Status, o.InitiatedFrom, o.FillQuantity, o.FilledQuantity, o.Quantity, o.FillPrice,
                o.InstrumentCurrency, o.NetValue, o.WalletCurrency, o.FxRate, o.RealisedProfitLoss, o.Taxes,
            })
            .ToListAsync(cancellationToken);

        return [.. orders
            .Select(o => new OrderRow(
                o.Id, o.Account, o.OrderId, o.CreatedAt, o.FilledAt, o.Ticker, o.InstrumentName, o.Isin,
                o.Side, o.Type, o.Status, o.InitiatedFrom,
                o.FillQuantity ?? o.FilledQuantity ?? o.Quantity,
                o.FillPrice, o.InstrumentCurrency, o.NetValue, o.WalletCurrency, o.FxRate, o.RealisedProfitLoss,
                o.Taxes.Sum(t => t.Quantity)))
            .OrderByDescending(o => o.FilledAt ?? o.CreatedAt)
            .ThenByDescending(o => o.Id)];
    }

    public async Task<IReadOnlyList<CashRow>> GetCashAsync(int? accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CashTransactions.AsNoTracking()
            .Where(t => accountId == null || t.AccountId == accountId)
            .OrderByDescending(t => t.DateTime).ThenByDescending(t => t.Id)
            .Select(t => new CashRow(t.Id, t.Account!.Name, t.DateTime, t.Type, t.Amount, t.Currency, t.Reference, t.Sources))
            .ToListAsync(cancellationToken);
    }
}
