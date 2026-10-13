namespace Arisoul.T212Ext.Core.Domain;

/// <summary>Where a row came from. A row seen in both the API and a CSV report has both flags.</summary>
[Flags]
public enum DataSources
{
    None = 0,
    Api = 1,
    Csv = 2,
}

/// <summary>
/// A Trading 212 account (Invest or Stocks ISA) as configured under <c>T212:Accounts</c>.
/// Credentials are never stored in the database: they stay in configuration.
/// </summary>
public class BrokerAccount
{
    public int Id { get; set; }

    /// <summary>The configuration key, e.g. "invest". Unique.</summary>
    public required string Key { get; set; }

    public required string Name { get; set; }
    public required string Environment { get; set; }

    /// <summary>Trading 212's own account number, learned from the account summary.</summary>
    public long? ExternalId { get; set; }

    /// <summary>Primary currency (ISO 4217), learned from the account summary.</summary>
    public string? Currency { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>An instrument from Trading 212's metadata. Shared by all accounts.</summary>
public class Instrument
{
    /// <summary>Trading 212's ticker, e.g. "AAPL_US_EQ". Primary key.</summary>
    public required string Ticker { get; set; }

    public string? Isin { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Type { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset? AddedOn { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A dividend (or other distribution) paid into an account.</summary>
public class Dividend
{
    public long Id { get; set; }
    public int AccountId { get; set; }
    public BrokerAccount? Account { get; set; }

    /// <summary>Trading 212's reference; unique per account. CSV rows without one get a "csv:" reference until the API confirms them.</summary>
    public required string Reference { get; set; }

    public DataSources Sources { get; set; }

    /// <summary>
    /// The CSV row this dividend was matched to (its ID, or a "csv:" hash when it has none). A row
    /// matched once is never matched to another CSV row, and importing the same report again finds it here.
    /// </summary>
    public string? CsvReference { get; set; }

    /// <summary>Trading 212's ticker when known (from the API or the instruments list); otherwise the CSV's plain symbol.</summary>
    public required string Ticker { get; set; }

    public string? Isin { get; set; }
    public string? InstrumentName { get; set; }

    /// <summary>ORDINARY, RETURN_OF_CAPITAL, …; CSV rows keep the text in brackets, e.g. "Ordinary".</summary>
    public required string Type { get; set; }

    public DateTimeOffset PaidOn { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>
    /// Gross amount per share, in <see cref="InstrumentCurrency"/>. Exact from the API; for rows only
    /// seen in a CSV report it is derived (net per share + withholding per share, rounded to cents).
    /// </summary>
    public decimal? GrossAmountPerShare { get; set; }

    /// <summary>
    /// Net amount per share, in <see cref="InstrumentCurrency"/>: the "Price / share" of a CSV report,
    /// which is the dividend after withholding. With the API's gross it gives the exact tax withheld.
    /// </summary>
    public decimal? NetAmountPerShare { get; set; }

    public string? InstrumentCurrency { get; set; }

    /// <summary>Net amount received, in <see cref="Currency"/> (the account's currency).</summary>
    public decimal Amount { get; set; }

    public required string Currency { get; set; }
    public decimal? AmountInEuro { get; set; }

    /// <summary>Tax withheld at source. Only CSV reports carry it.</summary>
    public decimal? WithholdingTax { get; set; }

    public string? WithholdingTaxCurrency { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}

/// <summary>An order from the order history, with its fill when it executed.</summary>
public class HistoricalOrder
{
    public long Id { get; set; }
    public int AccountId { get; set; }
    public BrokerAccount? Account { get; set; }

    public long OrderId { get; set; }
    public long? FillId { get; set; }

    public required string Ticker { get; set; }
    public string? Isin { get; set; }
    public string? InstrumentName { get; set; }
    public string? InstrumentCurrency { get; set; }

    public string? Side { get; set; }
    public string? Type { get; set; }
    public string? Strategy { get; set; }
    public string? Status { get; set; }
    public string? InitiatedFrom { get; set; }
    public string? TimeInForce { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }

    public decimal? Quantity { get; set; }
    public decimal? Value { get; set; }
    public decimal? FilledQuantity { get; set; }
    public decimal? FilledValue { get; set; }
    public decimal? LimitPrice { get; set; }
    public decimal? StopPrice { get; set; }

    public DateTimeOffset? FilledAt { get; set; }
    public decimal? FillPrice { get; set; }
    public decimal? FillQuantity { get; set; }

    /// <summary>TRADE, STOCK_SPLIT, SPIN_OFF, …</summary>
    public string? FillType { get; set; }

    public string? TradingMethod { get; set; }

    /// <summary>Currency of <see cref="NetValue"/> and <see cref="RealisedProfitLoss"/> (the account's).</summary>
    public string? WalletCurrency { get; set; }

    public decimal? FxRate { get; set; }
    public decimal? NetValue { get; set; }
    public decimal? RealisedProfitLoss { get; set; }

    public List<OrderTax> Taxes { get; set; } = [];

    public DateTimeOffset ImportedAt { get; set; }
}

public class OrderTax
{
    public required string Name { get; set; }
    public decimal Quantity { get; set; }
    public string? Currency { get; set; }
    public DateTimeOffset? ChargedAt { get; set; }
}

/// <summary>Cash moving in or out: deposits, withdrawals, fees, transfers, interest.</summary>
/// <remarks>Signs are normalised: deposits and interest are positive, withdrawals and fees negative.</remarks>
public class CashTransaction
{
    public long Id { get; set; }
    public int AccountId { get; set; }
    public BrokerAccount? Account { get; set; }

    public required string Reference { get; set; }
    public DataSources Sources { get; set; }

    /// <summary>The CSV row this movement was matched to; see <see cref="Dividend.CsvReference"/>.</summary>
    public string? CsvReference { get; set; }

    /// <summary>WITHDRAW, DEPOSIT, FEE, TRANSFER, INTEREST_ON_FREE_CASH, LENDING_INTEREST, DIVIDEND_ADJUSTMENT.</summary>
    public required string Type { get; set; }

    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset DateTime { get; set; }

    /// <summary>The note of the CSV row, e.g. "2024 US dividends withholding tax adjustment".</summary>
    public string? Note { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}

/// <summary>A euro reference rate of the European Central Bank: units of <see cref="Currency"/> per 1 EUR.</summary>
public class ExchangeRate
{
    public required string Currency { get; set; }
    public DateOnly Date { get; set; }
    public decimal PerEuro { get; set; }
}

/// <summary>
/// A stock split (or reverse split) entered by the person: Trading 212 changes the quantity held
/// without recording an order, so the FIFO of capital gains needs it.
/// </summary>
public class StockSplit
{
    public long Id { get; set; }
    public required string Isin { get; set; }

    /// <summary>The first day the new quantity applies.</summary>
    public DateOnly Date { get; set; }

    /// <summary>New shares per old share: 2 for a 2-for-1 split, 0.1 for a 1-for-10 reverse split.</summary>
    public decimal Ratio { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>What the person set for an instrument, by ISIN. Shared by all accounts.</summary>
public class InstrumentSetting
{
    public required string Isin { get; set; }

    /// <summary>
    /// The country the income comes from, for the tax report, when the ISIN's country is not it
    /// (ISO 3166 alpha-2, e.g. "US" for an ADR issued elsewhere). Null: the ISIN's country.
    /// </summary>
    public string? SourceCountry { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// The account's value and positions on one day. The API only gives the current state, so the
/// history of the portfolio exists only because these are taken. One per account per day; the
/// last sync of the day wins.
/// </summary>
public class PortfolioSnapshot
{
    public long Id { get; set; }
    public int AccountId { get; set; }
    public BrokerAccount? Account { get; set; }

    public DateOnly Date { get; set; }
    public DateTimeOffset TakenAt { get; set; }
    public string? Currency { get; set; }

    public decimal? TotalValue { get; set; }
    public decimal? CashAvailable { get; set; }
    public decimal? CashInPies { get; set; }
    public decimal? CashReserved { get; set; }
    public decimal? InvestmentsValue { get; set; }
    public decimal? TotalCost { get; set; }
    public decimal? UnrealizedProfitLoss { get; set; }
    public decimal? RealizedProfitLoss { get; set; }

    public List<PositionSnapshot> Positions { get; set; } = [];
}

public class PositionSnapshot
{
    public required string Ticker { get; set; }
    public string? Isin { get; set; }
    public string? Name { get; set; }
    public string? InstrumentCurrency { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }

    public decimal Quantity { get; set; }
    public decimal? QuantityInPies { get; set; }

    /// <summary>In <see cref="InstrumentCurrency"/>.</summary>
    public decimal? AveragePricePaid { get; set; }

    /// <summary>In <see cref="InstrumentCurrency"/>.</summary>
    public decimal? CurrentPrice { get; set; }

    /// <summary>Currency of the values below (the account's).</summary>
    public string? WalletCurrency { get; set; }

    public decimal? CurrentValue { get; set; }
    public decimal? TotalCost { get; set; }
    public decimal? UnrealizedProfitLoss { get; set; }
    public decimal? FxImpact { get; set; }
}

public enum SyncJob
{
    Snapshot,
    Instruments,
    Dividends,
    Orders,
    Transactions,
    ExchangeRates,
}

/// <summary>Progress of one history job of one account.</summary>
public class SyncState
{
    public int AccountId { get; set; }
    public SyncJob Job { get; set; }

    /// <summary>
    /// True once a sync has walked the history to its last page. Until then a sync never stops
    /// early, so an interrupted first sync is completed by the next one.
    /// </summary>
    public bool HistoryComplete { get; set; }

    public DateTimeOffset? LastSuccessAt { get; set; }
}

/// <summary>One run of one job, kept as a log.</summary>
public class SyncRun
{
    public long Id { get; set; }

    /// <summary>Null for jobs that are not about one account (instruments).</summary>
    public int? AccountId { get; set; }

    public BrokerAccount? Account { get; set; }
    public SyncJob Job { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool Succeeded { get; set; }
    public int ItemsAdded { get; set; }
    public int ItemsUpdated { get; set; }
    public string? Error { get; set; }
}
