using Arisoul.T212Ext.Client.Models;

namespace Arisoul.T212Ext.Client;

/// <summary>One page of a history endpoint. <see cref="HasMore"/> is false on the last page.</summary>
public sealed record T212Page<T>(IReadOnlyList<T> Items, bool HasMore);

/// <summary>
/// Read access to one Trading 212 account. Waits for rate limits and retries transient failures;
/// anything else surfaces as <see cref="T212ApiException"/>.
/// </summary>
public interface IT212Api
{
    Task<AccountSummary> GetAccountSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Position>> GetPositionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradableInstrument>> GetInstrumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Paid dividends, newest first, page by page.</summary>
    IAsyncEnumerable<T212Page<HistoryDividendItem>> GetDividendPagesAsync(string? ticker = null, CancellationToken cancellationToken = default);

    /// <summary>Historical orders with their fills, newest first, page by page.</summary>
    IAsyncEnumerable<T212Page<HistoricalOrder>> GetOrderPagesAsync(string? ticker = null, CancellationToken cancellationToken = default);

    /// <summary>Deposits, withdrawals, fees, transfers and interest, newest first, page by page.</summary>
    IAsyncEnumerable<T212Page<HistoryTransactionItem>> GetTransactionPagesAsync(DateTimeOffset? from = null, CancellationToken cancellationToken = default);

    /// <summary>Asks for a CSV report; returns its id. The report is ready when <see cref="GetReportsAsync"/> says Finished.</summary>
    Task<long> RequestReportAsync(ReportRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportStatus>> GetReportsAsync(CancellationToken cancellationToken = default);

    /// <summary>Downloads a finished report. The link points to another host, so no credentials are sent.</summary>
    Task<Stream> DownloadReportAsync(Uri downloadLink, CancellationToken cancellationToken = default);
}
