using System.Runtime.CompilerServices;
using Arisoul.T212Ext.Client;
using Arisoul.T212Ext.Client.Models;
using Arisoul.T212Ext.Core.Rates;

namespace Arisoul.T212Ext.Core.Tests.Infrastructure;

/// <summary>An in-memory Trading 212 account. History lists are newest first, split into pages of <see cref="PageSize"/>.</summary>
public sealed class FakeT212Api : IT212Api
{
    public int PageSize { get; set; } = 2;

    public AccountSummary Summary { get; set; } = new() { Id = 1, Currency = "EUR", TotalValue = 0 };
    public List<Position> Positions { get; set; } = [];
    public List<TradableInstrument> Instruments { get; set; } = [];
    public List<HistoryDividendItem> Dividends { get; set; } = [];
    public List<HistoricalOrder> Orders { get; set; } = [];
    public List<HistoryTransactionItem> Transactions { get; set; } = [];

    /// <summary>Thrown when this page (1-based) of any history is asked for, once.</summary>
    public (int Page, Exception Error)? FailOnPage { get; set; }

    /// <summary>Thrown by every call, e.g. a refused key.</summary>
    public Exception? FailEverything { get; set; }

    /// <summary>Thrown by the order history only.</summary>
    public Exception? FailOrders { get; set; }

    public int PagesRead { get; private set; }
    public int InstrumentCalls { get; private set; }
    public int SummaryCalls { get; private set; }

    public Task<AccountSummary> GetAccountSummaryAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();
        SummaryCalls++;
        return Task.FromResult(Summary);
    }

    public Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();
        return Task.FromResult<IReadOnlyList<Position>>(Positions);
    }

    public Task<IReadOnlyList<TradableInstrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();
        InstrumentCalls++;
        return Task.FromResult<IReadOnlyList<TradableInstrument>>(Instruments);
    }

    public IAsyncEnumerable<T212Page<HistoryDividendItem>> GetDividendPagesAsync(string? ticker = null, CancellationToken cancellationToken = default) =>
        PagesAsync(Dividends, cancellationToken);

    public IAsyncEnumerable<T212Page<HistoricalOrder>> GetOrderPagesAsync(string? ticker = null, CancellationToken cancellationToken = default) =>
        FailOrders is not null ? Throwing<HistoricalOrder>(FailOrders) : PagesAsync(Orders, cancellationToken);

    public IAsyncEnumerable<T212Page<HistoryTransactionItem>> GetTransactionPagesAsync(DateTimeOffset? from = null, CancellationToken cancellationToken = default) =>
        PagesAsync(Transactions, cancellationToken);

    public Task<long> RequestReportAsync(ReportRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<ReportStatus>> GetReportsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Stream> DownloadReportAsync(Uri downloadLink, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    private async IAsyncEnumerable<T212Page<T>> PagesAsync<T>(List<T> items, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowIfFailing();
        var pageCount = Math.Max(1, (int)Math.Ceiling(items.Count / (double)PageSize));

        for (var page = 1; page <= pageCount; page++)
        {
            await Task.Yield();
            if (FailOnPage is { } failure && failure.Page == page)
            {
                FailOnPage = null;
                throw failure.Error;
            }

            PagesRead++;
            yield return new T212Page<T>([.. items.Skip((page - 1) * PageSize).Take(PageSize)], page < pageCount);
        }
    }

    private static async IAsyncEnumerable<T212Page<T>> Throwing<T>(Exception error)
    {
        await Task.Yield();
        throw error;
#pragma warning disable CS0162 // An iterator needs a yield.
        yield break;
#pragma warning restore CS0162
    }

    private void ThrowIfFailing()
    {
        if (FailEverything is not null)
            throw FailEverything;
    }

    public static HistoryDividendItem Dividend(string reference, DateTimeOffset paidOn, decimal amount, string isin = "US0378331005", decimal quantity = 10) => new()
    {
        Reference = reference,
        Ticker = "AAPL_US_EQ",
        Instrument = new InstrumentRef { Ticker = "AAPL_US_EQ", Isin = isin, Name = "Apple", Currency = "USD" },
        Type = "ORDINARY",
        PaidOn = paidOn,
        Quantity = quantity,
        GrossAmountPerShare = 0.25m,
        TickerCurrency = "USD",
        Amount = amount,
        Currency = "EUR",
        AmountInEuro = amount,
    };
}

/// <summary>Hands out one fake per account key.</summary>
public sealed class FakeT212ApiFactory : IT212ApiFactory
{
    public Dictionary<string, FakeT212Api> Apis { get; } = new(StringComparer.Ordinal);

    public IT212Api Create(T212Account account) =>
        Apis.TryGetValue(account.Key, out var api) ? api : throw new InvalidOperationException($"No fake for account {account.Key}.");
}

/// <summary>A clock that moves only when told to.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Rates from memory: every day from <see cref="From"/>, 1.1 units per euro unless set in <see cref="Rates"/>.</summary>
public sealed class FakeRateSource : IExchangeRateSource
{
    public Dictionary<(string Currency, DateOnly Date), decimal> Rates { get; } = [];
    public List<(string Currency, DateOnly From, DateOnly To)> Calls { get; } = [];

    public Task<IReadOnlyList<(DateOnly Date, decimal PerEuro)>> GetDailyRatesAsync(string currency, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        Calls.Add((currency, from, to));
        var days = new List<(DateOnly, decimal)>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            // Business days only, like the ECB.
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            days.Add((day, Rates.GetValueOrDefault((currency, day), 1.1m)));
        }
        return Task.FromResult<IReadOnlyList<(DateOnly, decimal)>>(days);
    }
}
