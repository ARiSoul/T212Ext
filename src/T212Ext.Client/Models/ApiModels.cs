namespace Arisoul.T212Ext.Client.Models;

// Response shapes of the Trading 212 public API v0 (https://docs.trading212.com).
// Enums from the API are kept as strings: Trading 212 adds values without notice
// (the dividend types list grew to ~60), and an unknown value must never break a sync.
// Every property is nullable because the API documents no required fields.

public sealed class AccountSummary
{
    public long? Id { get; init; }
    public string? Currency { get; init; }
    public decimal? TotalValue { get; init; }
    public AccountCash? Cash { get; init; }
    public AccountInvestments? Investments { get; init; }
}

public sealed class AccountCash
{
    public decimal? AvailableToTrade { get; init; }
    public decimal? InPies { get; init; }
    public decimal? ReservedForOrders { get; init; }
}

public sealed class AccountInvestments
{
    public decimal? CurrentValue { get; init; }
    public decimal? TotalCost { get; init; }
    public decimal? RealizedProfitLoss { get; init; }
    public decimal? UnrealizedProfitLoss { get; init; }
}

/// <summary>The short instrument reference embedded in positions, dividends and orders.</summary>
public sealed class InstrumentRef
{
    public string? Ticker { get; init; }
    public string? Isin { get; init; }
    public string? Name { get; init; }
    public string? Currency { get; init; }
}

public sealed class Position
{
    public InstrumentRef? Instrument { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? QuantityAvailableForTrading { get; init; }
    public decimal? QuantityInPies { get; init; }

    /// <summary>In instrument currency.</summary>
    public decimal? AveragePricePaid { get; init; }

    /// <summary>In instrument currency.</summary>
    public decimal? CurrentPrice { get; init; }

    public PositionWalletImpact? WalletImpact { get; init; }
}

public sealed class PositionWalletImpact
{
    public string? Currency { get; init; }
    public decimal? CurrentValue { get; init; }
    public decimal? TotalCost { get; init; }
    public decimal? UnrealizedProfitLoss { get; init; }
    public decimal? FxImpact { get; init; }
}

/// <summary>An instrument from <c>/metadata/instruments</c>.</summary>
public sealed class TradableInstrument
{
    public string? Ticker { get; init; }
    public string? Isin { get; init; }
    public string? Name { get; init; }
    public string? ShortName { get; init; }
    public string? Type { get; init; }
    public string? CurrencyCode { get; init; }
    public DateTimeOffset? AddedOn { get; init; }
    public bool? ExtendedHours { get; init; }
    public decimal? MaxOpenQuantity { get; init; }
    public long? WorkingScheduleId { get; init; }
}

public sealed class HistoryDividendItem
{
    public string? Reference { get; init; }
    public string? Ticker { get; init; }
    public InstrumentRef? Instrument { get; init; }

    /// <summary>One of ORDINARY, RETURN_OF_CAPITAL, … (see the API spec).</summary>
    public string? Type { get; init; }

    public DateTimeOffset? PaidOn { get; init; }
    public decimal? Quantity { get; init; }

    /// <summary>In instrument currency (<see cref="TickerCurrency"/>).</summary>
    public decimal? GrossAmountPerShare { get; init; }

    public string? TickerCurrency { get; init; }

    /// <summary>Net amount received, in the account's primary currency (<see cref="Currency"/>).</summary>
    public decimal? Amount { get; init; }

    public string? Currency { get; init; }
    public decimal? AmountInEuro { get; init; }
}

/// <summary>One item of <c>/history/orders</c>: the order and, when it executed, its fill.</summary>
public sealed class HistoricalOrder
{
    public Order? Order { get; init; }
    public Fill? Fill { get; init; }
}

public sealed class Order
{
    public long? Id { get; init; }
    public string? Ticker { get; init; }
    public InstrumentRef? Instrument { get; init; }
    public string? Side { get; init; }
    public string? Type { get; init; }
    public string? Strategy { get; init; }
    public string? Status { get; init; }
    public string? InitiatedFrom { get; init; }
    public string? TimeInForce { get; init; }
    public bool? ExtendedHours { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? Value { get; init; }
    public decimal? FilledQuantity { get; init; }
    public decimal? FilledValue { get; init; }
    public decimal? LimitPrice { get; init; }
    public decimal? StopPrice { get; init; }
}

public sealed class Fill
{
    public long? Id { get; init; }
    public DateTimeOffset? FilledAt { get; init; }
    public decimal? Price { get; init; }
    public decimal? Quantity { get; init; }
    public string? TradingMethod { get; init; }

    /// <summary>TRADE, STOCK_SPLIT, SPIN_OFF, …</summary>
    public string? Type { get; init; }

    public FillWalletImpact? WalletImpact { get; init; }
}

public sealed class FillWalletImpact
{
    public string? Currency { get; init; }
    public decimal? FxRate { get; init; }
    public decimal? NetValue { get; init; }
    public decimal? RealisedProfitLoss { get; init; }
    public IReadOnlyList<Tax>? Taxes { get; init; }
}

public sealed class Tax
{
    /// <summary>COMMISSION_TURNOVER, CURRENCY_CONVERSION_FEE, STAMP_DUTY, …</summary>
    public string? Name { get; init; }

    public decimal? Quantity { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset? ChargedAt { get; init; }
}

public sealed class HistoryTransactionItem
{
    public string? Reference { get; init; }

    /// <summary>WITHDRAW, DEPOSIT, FEE, TRANSFER, INTEREST_ON_FREE_CASH, LENDING_INTEREST.</summary>
    public string? Type { get; init; }

    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset? DateTime { get; init; }
}

public sealed class ReportRequest
{
    public required DateTimeOffset TimeFrom { get; init; }
    public required DateTimeOffset TimeTo { get; init; }
    public ReportDataIncluded DataIncluded { get; init; } = new();
}

public sealed class ReportDataIncluded
{
    public bool IncludeDividends { get; init; } = true;
    public bool IncludeInterest { get; init; } = true;
    public bool IncludeOrders { get; init; } = true;
    public bool IncludeTransactions { get; init; } = true;
}

public sealed class ReportStatus
{
    public long? ReportId { get; init; }

    /// <summary>Queued, Processing, Running, Canceled, Failed or Finished.</summary>
    public string? Status { get; init; }

    public string? DownloadLink { get; init; }
    public DateTimeOffset? TimeFrom { get; init; }
    public DateTimeOffset? TimeTo { get; init; }
    public ReportDataIncluded? DataIncluded { get; init; }
}

internal sealed class EnqueuedReport
{
    public long? ReportId { get; init; }
}

internal sealed class PaginatedResponse<T>
{
    public IReadOnlyList<T>? Items { get; init; }
    public string? NextPagePath { get; init; }
}
