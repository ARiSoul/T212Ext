using Arisoul.T212Ext.Client;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Api = Arisoul.T212Ext.Client.Models;

namespace Arisoul.T212Ext.Core.Sync;

/// <summary>How many rows a job added and updated.</summary>
public readonly record struct JobResult(int Added, int Updated)
{
    public static JobResult operator +(JobResult a, JobResult b) => new(a.Added + b.Added, a.Updated + b.Updated);
}

/// <summary>
/// Copies one account's data from the API into the database, one job at a time.
/// </summary>
/// <remarks>
/// History jobs read newest first and save page by page, so an interrupted sync keeps what it read.
/// Once a job has reached the end of the history (<see cref="SyncState.HistoryComplete"/>), later
/// syncs stop at the first page that brings nothing new.
/// </remarks>
public sealed class AccountSynchronizer(
    IDbContextFactory<T212ExtDbContext> dbFactory,
    TimeProvider time,
    ILogger<AccountSynchronizer> logger)
{
    public async Task<JobResult> SyncSnapshotAsync(BrokerAccount account, IT212Api api, CancellationToken cancellationToken)
    {
        var summary = await api.GetAccountSummaryAsync(cancellationToken);
        var positions = await api.GetPositionsAsync(cancellationToken);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var stored = await db.Accounts.SingleAsync(a => a.Id == account.Id, cancellationToken);
        stored.ExternalId = summary.Id ?? stored.ExternalId;
        stored.Currency = summary.Currency ?? stored.Currency;
        account.Currency = stored.Currency;

        var snapshot = await db.Snapshots.Include(s => s.Positions)
            .SingleOrDefaultAsync(s => s.AccountId == account.Id && s.Date == today, cancellationToken);
        var isNew = snapshot is null;
        if (snapshot is null)
        {
            snapshot = new PortfolioSnapshot { AccountId = account.Id, Date = today };
            db.Snapshots.Add(snapshot);
        }

        snapshot.TakenAt = now;
        snapshot.Currency = summary.Currency;
        snapshot.TotalValue = summary.TotalValue;
        snapshot.CashAvailable = summary.Cash?.AvailableToTrade;
        snapshot.CashInPies = summary.Cash?.InPies;
        snapshot.CashReserved = summary.Cash?.ReservedForOrders;
        snapshot.InvestmentsValue = summary.Investments?.CurrentValue;
        snapshot.TotalCost = summary.Investments?.TotalCost;
        snapshot.UnrealizedProfitLoss = summary.Investments?.UnrealizedProfitLoss;
        snapshot.RealizedProfitLoss = summary.Investments?.RealizedProfitLoss;
        snapshot.Positions = [.. positions.Where(p => p.Instrument?.Ticker is not null).Select(ToPositionSnapshot)];

        await db.SaveChangesAsync(cancellationToken);
        return isNew ? new JobResult(1, 0) : new JobResult(0, 1);
    }

    /// <summary>Refreshes the instrument list (shared by all accounts).</summary>
    public async Task<JobResult> SyncInstrumentsAsync(IT212Api api, CancellationToken cancellationToken)
    {
        var instruments = await api.GetInstrumentsAsync(cancellationToken);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Instruments.ToDictionaryAsync(i => i.Ticker, StringComparer.Ordinal, cancellationToken);
        var now = time.GetUtcNow();
        var result = new JobResult();

        foreach (var item in instruments)
        {
            if (string.IsNullOrWhiteSpace(item.Ticker))
                continue;

            if (!existing.TryGetValue(item.Ticker, out var instrument))
            {
                instrument = new Instrument { Ticker = item.Ticker };
                existing.Add(item.Ticker, instrument);
                db.Instruments.Add(instrument);
                result += new JobResult(1, 0);
            }
            else if (instrument.Isin == item.Isin && instrument.Name == item.Name && instrument.ShortName == item.ShortName
                && instrument.Type == item.Type && instrument.Currency == item.CurrencyCode && instrument.AddedOn == item.AddedOn)
            {
                continue;
            }
            else
            {
                result += new JobResult(0, 1);
            }

            instrument.Isin = item.Isin;
            instrument.Name = item.Name;
            instrument.ShortName = item.ShortName;
            instrument.Type = item.Type;
            instrument.Currency = item.CurrencyCode;
            instrument.AddedOn = item.AddedOn;
            instrument.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public Task<JobResult> SyncDividendsAsync(BrokerAccount account, IT212Api api, bool fullHistory, CancellationToken cancellationToken) =>
        SyncHistoryAsync(account.Id, SyncJob.Dividends, api.GetDividendPagesAsync(cancellationToken: cancellationToken),
            (db, items, ct) => SaveDividendsAsync(db, account, items, ct), fullHistory, cancellationToken);

    public Task<JobResult> SyncOrdersAsync(BrokerAccount account, IT212Api api, bool fullHistory, CancellationToken cancellationToken) =>
        SyncHistoryAsync(account.Id, SyncJob.Orders, api.GetOrderPagesAsync(cancellationToken: cancellationToken),
            (db, items, ct) => SaveOrdersAsync(db, account, items, ct), fullHistory, cancellationToken);

    public Task<JobResult> SyncTransactionsAsync(BrokerAccount account, IT212Api api, bool fullHistory, CancellationToken cancellationToken) =>
        SyncHistoryAsync(account.Id, SyncJob.Transactions, api.GetTransactionPagesAsync(cancellationToken: cancellationToken),
            (db, items, ct) => SaveTransactionsAsync(db, account, items, ct), fullHistory, cancellationToken);

    private async Task<JobResult> SyncHistoryAsync<T>(
        int accountId,
        SyncJob job,
        IAsyncEnumerable<T212Page<T>> pages,
        Func<T212ExtDbContext, IReadOnlyList<T>, CancellationToken, Task<JobResult>> savePage,
        bool fullHistory,
        CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var state = await db.SyncStates.FindAsync([accountId, job], cancellationToken);
        if (state is null)
        {
            state = new SyncState { AccountId = accountId, Job = job };
            db.SyncStates.Add(state);
        }

        var total = new JobResult();
        var reachedEnd = false;

        await foreach (var page in pages.WithCancellation(cancellationToken))
        {
            var result = await savePage(db, page.Items, cancellationToken);
            total += result;

            if (!page.HasMore)
            {
                reachedEnd = true;
                break;
            }

            if (!fullHistory && state.HistoryComplete && result == default && page.Items.Count > 0)
            {
                logger.LogDebug("{Job} of account {AccountId}: page with nothing new, stopping", job, accountId);
                break;
            }
        }

        if (reachedEnd)
            state.HistoryComplete = true;

        state.LastSuccessAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return total;
    }

    private async Task<JobResult> SaveDividendsAsync(T212ExtDbContext db, BrokerAccount account, IReadOnlyList<Api.HistoryDividendItem> page, CancellationToken cancellationToken)
    {
        var items = page.Where(i => !string.IsNullOrWhiteSpace(i.Reference) && i.PaidOn is not null).ToList();
        if (items.Count < page.Count)
            logger.LogWarning("Skipped {Count} dividends without reference or date (account {Account})", page.Count - items.Count, account.Key);
        if (items.Count == 0)
            return default;

        var references = items.Select(i => i.Reference!).Distinct().ToList();
        var known = (await db.Dividends
                .Where(d => d.AccountId == account.Id && references.Contains(d.Reference))
                .Select(d => d.Reference)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var unknown = items.Where(i => !known.Contains(i.Reference!)).ToList();
        if (unknown.Count == 0)
            return default;

        // Rows imported from CSV that the API has not confirmed yet: the same dividend, without the API's reference.
        var from = unknown.Min(i => i.PaidOn!.Value) - DividendMatcher.DateTolerance;
        var to = unknown.Max(i => i.PaidOn!.Value) + DividendMatcher.DateTolerance;
        var csvOnly = await db.Dividends
            .Where(d => d.AccountId == account.Id && d.Sources == DataSources.Csv && d.PaidOn >= from && d.PaidOn <= to)
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow();
        var result = new JobResult();

        foreach (var item in unknown)
        {
            if (!known.Add(item.Reference!))
                continue;

            var isin = item.Instrument?.Isin;
            var ticker = item.Ticker ?? item.Instrument?.Ticker ?? isin ?? "?";
            var match = DividendMatcher.FindMatch(csvOnly, isin, ticker, item.PaidOn!.Value, item.Quantity ?? 0, item.Amount ?? 0);

            var dividend = match ?? new Dividend
            {
                AccountId = account.Id,
                Reference = item.Reference!,
                Ticker = ticker,
                Type = item.Type ?? "UNKNOWN",
                Currency = item.Currency ?? account.Currency ?? "",
                ImportedAt = now,
            };

            dividend.Reference = item.Reference!;
            dividend.Sources |= DataSources.Api;
            dividend.Ticker = ticker;
            dividend.Isin = isin ?? dividend.Isin;
            dividend.InstrumentName = item.Instrument?.Name ?? dividend.InstrumentName;
            dividend.Type = item.Type ?? dividend.Type;
            dividend.PaidOn = item.PaidOn.Value;
            dividend.Quantity = item.Quantity ?? dividend.Quantity;
            dividend.GrossAmountPerShare = item.GrossAmountPerShare ?? dividend.GrossAmountPerShare;
            dividend.InstrumentCurrency = item.TickerCurrency ?? item.Instrument?.Currency ?? dividend.InstrumentCurrency;
            dividend.Amount = item.Amount ?? dividend.Amount;
            dividend.Currency = item.Currency ?? dividend.Currency;
            dividend.AmountInEuro = item.AmountInEuro ?? dividend.AmountInEuro;

            if (match is null)
            {
                db.Dividends.Add(dividend);
                result += new JobResult(1, 0);
            }
            else
            {
                csvOnly.Remove(match);
                result += new JobResult(0, 1);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<JobResult> SaveOrdersAsync(T212ExtDbContext db, BrokerAccount account, IReadOnlyList<Api.HistoricalOrder> page, CancellationToken cancellationToken)
    {
        var items = page.Where(i => i.Order?.Id is not null).ToList();
        if (items.Count < page.Count)
            logger.LogWarning("Skipped {Count} orders without id (account {Account})", page.Count - items.Count, account.Key);
        if (items.Count == 0)
            return default;

        var orderIds = items.Select(i => i.Order!.Id!.Value).Distinct().ToList();
        var known = (await db.Orders
                .Where(o => o.AccountId == account.Id && orderIds.Contains(o.OrderId))
                .Select(o => new { o.OrderId, o.FillId })
                .ToListAsync(cancellationToken))
            .Select(o => (o.OrderId, o.FillId))
            .ToHashSet();

        var now = time.GetUtcNow();
        var added = 0;

        foreach (var item in items)
        {
            var order = item.Order!;
            var fill = item.Fill;
            if (!known.Add((order.Id!.Value, fill?.Id)))
                continue;

            db.Orders.Add(new HistoricalOrder
            {
                AccountId = account.Id,
                OrderId = order.Id.Value,
                FillId = fill?.Id,
                Ticker = order.Ticker ?? order.Instrument?.Ticker ?? "?",
                Isin = order.Instrument?.Isin,
                InstrumentName = order.Instrument?.Name,
                InstrumentCurrency = order.Instrument?.Currency ?? order.Currency,
                Side = order.Side,
                Type = order.Type,
                Strategy = order.Strategy,
                Status = order.Status,
                InitiatedFrom = order.InitiatedFrom,
                TimeInForce = order.TimeInForce,
                CreatedAt = order.CreatedAt,
                Quantity = order.Quantity,
                Value = order.Value,
                FilledQuantity = order.FilledQuantity,
                FilledValue = order.FilledValue,
                LimitPrice = order.LimitPrice,
                StopPrice = order.StopPrice,
                FilledAt = fill?.FilledAt,
                FillPrice = fill?.Price,
                FillQuantity = fill?.Quantity,
                FillType = fill?.Type,
                TradingMethod = fill?.TradingMethod,
                WalletCurrency = fill?.WalletImpact?.Currency,
                FxRate = fill?.WalletImpact?.FxRate,
                NetValue = fill?.WalletImpact?.NetValue,
                RealisedProfitLoss = fill?.WalletImpact?.RealisedProfitLoss,
                Taxes = [.. (fill?.WalletImpact?.Taxes ?? []).Select(t => new OrderTax
                {
                    Name = t.Name ?? "UNKNOWN",
                    Quantity = t.Quantity ?? 0,
                    Currency = t.Currency,
                    ChargedAt = t.ChargedAt,
                })],
                ImportedAt = now,
            });
            added++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new JobResult(added, 0);
    }

    private async Task<JobResult> SaveTransactionsAsync(T212ExtDbContext db, BrokerAccount account, IReadOnlyList<Api.HistoryTransactionItem> page, CancellationToken cancellationToken)
    {
        var items = page.Where(i => !string.IsNullOrWhiteSpace(i.Reference) && i.DateTime is not null && i.Type is not null && i.Amount is not null).ToList();
        if (items.Count < page.Count)
            logger.LogWarning("Skipped {Count} incomplete transactions (account {Account})", page.Count - items.Count, account.Key);
        if (items.Count == 0)
            return default;

        var references = items.Select(i => i.Reference!).Distinct().ToList();
        var known = (await db.CashTransactions
                .Where(t => t.AccountId == account.Id && references.Contains(t.Reference))
                .Select(t => t.Reference)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var unknown = items.Where(i => !known.Contains(i.Reference!)).ToList();
        if (unknown.Count == 0)
            return default;

        var from = unknown.Min(i => i.DateTime!.Value) - CashTransactionMatcher.DateTolerance;
        var to = unknown.Max(i => i.DateTime!.Value) + CashTransactionMatcher.DateTolerance;
        var csvOnly = await db.CashTransactions
            .Where(t => t.AccountId == account.Id && t.Sources == DataSources.Csv && t.DateTime >= from && t.DateTime <= to)
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNow();
        var result = new JobResult();

        foreach (var item in unknown)
        {
            if (!known.Add(item.Reference!))
                continue;

            var amount = CashTransactionTypes.NormaliseSign(item.Type!, item.Amount!.Value);
            var match = CashTransactionMatcher.FindMatch(csvOnly, item.Type!, amount, item.DateTime!.Value);
            if (match is not null)
            {
                match.Reference = item.Reference!;
                match.Sources |= DataSources.Api;
                match.DateTime = item.DateTime.Value;
                csvOnly.Remove(match);
                result += new JobResult(0, 1);
                continue;
            }

            db.CashTransactions.Add(new CashTransaction
            {
                AccountId = account.Id,
                Reference = item.Reference!,
                Sources = DataSources.Api,
                Type = item.Type!,
                Amount = amount,
                Currency = item.Currency ?? account.Currency ?? "",
                DateTime = item.DateTime.Value,
                ImportedAt = now,
            });
            result += new JobResult(1, 0);
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static PositionSnapshot ToPositionSnapshot(Api.Position position) => new()
    {
        Ticker = position.Instrument!.Ticker!,
        Isin = position.Instrument.Isin,
        Name = position.Instrument.Name,
        InstrumentCurrency = position.Instrument.Currency,
        OpenedAt = position.CreatedAt,
        Quantity = position.Quantity ?? 0,
        QuantityInPies = position.QuantityInPies,
        AveragePricePaid = position.AveragePricePaid,
        CurrentPrice = position.CurrentPrice,
        WalletCurrency = position.WalletImpact?.Currency,
        CurrentValue = position.WalletImpact?.CurrentValue,
        TotalCost = position.WalletImpact?.TotalCost,
        UnrealizedProfitLoss = position.WalletImpact?.UnrealizedProfitLoss,
        FxImpact = position.WalletImpact?.FxImpact,
    };
}
