namespace Arisoul.T212Ext.Core.Domain;

/// <summary>
/// Finds the same dividend seen through two sources. The API gives a reference; CSV reports
/// usually do not, so rows are matched on instrument, date, quantity and net amount.
/// </summary>
/// <remarks>
/// The date tolerance absorbs time zone differences between the API and the CSV. Two payments of
/// the same instrument a few days apart (an ordinary dividend and a return of capital on the same
/// day) are told apart by their amounts.
/// </remarks>
public static class DividendMatcher
{
    public static readonly TimeSpan DateTolerance = TimeSpan.FromDays(3);
    public const decimal QuantityTolerance = 0.0001m;
    public const decimal AmountTolerance = 0.02m;

    public static Dividend? FindMatch(IEnumerable<Dividend> candidates, string? isin, string? ticker, DateTimeOffset paidOn, decimal quantity, decimal amount) =>
        candidates
            .Where(c => SameInstrument(c, isin, ticker)
                && (c.PaidOn - paidOn).Duration() <= DateTolerance
                && Math.Abs(c.Quantity - quantity) <= QuantityTolerance
                && Math.Abs(c.Amount - amount) <= AmountTolerance)
            .OrderBy(c => (c.PaidOn - paidOn).Duration())
            .FirstOrDefault();

    private static bool SameInstrument(Dividend candidate, string? isin, string? ticker) =>
        !string.IsNullOrWhiteSpace(isin) && !string.IsNullOrWhiteSpace(candidate.Isin)
            ? string.Equals(candidate.Isin, isin, StringComparison.OrdinalIgnoreCase)
            : string.Equals(candidate.Ticker, ticker, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Finds the same cash movement seen through the API and a CSV report: same type and amount, close in time.</summary>
/// <remarks>
/// Both sources give times in UTC, so the tolerance is short: share lending pays every day, often the
/// same amount, and two days must never be taken for one.
/// </remarks>
public static class CashTransactionMatcher
{
    public static readonly TimeSpan DateTolerance = TimeSpan.FromHours(1);
    public const decimal AmountTolerance = 0.005m;

    public static CashTransaction? FindMatch(IEnumerable<CashTransaction> candidates, string type, decimal amount, DateTimeOffset dateTime) =>
        candidates
            .Where(c => c.Type == type
                && Math.Abs(c.Amount - amount) <= AmountTolerance
                && (c.DateTime - dateTime).Duration() <= DateTolerance)
            .OrderBy(c => (c.DateTime - dateTime).Duration())
            .FirstOrDefault();
}

public static class CashTransactionTypes
{
    public const string Deposit = "DEPOSIT";
    public const string Withdraw = "WITHDRAW";
    public const string Fee = "FEE";
    public const string Transfer = "TRANSFER";
    public const string InterestOnFreeCash = "INTEREST_ON_FREE_CASH";
    public const string LendingInterest = "LENDING_INTEREST";

    /// <summary>
    /// CSV reports only ("Dividend adjustment"): a correction of dividends already paid, such as a
    /// refund of US withholding tax. Not tied to one instrument. Keeps its sign.
    /// </summary>
    public const string DividendAdjustment = "DIVIDEND_ADJUSTMENT";

    /// <summary>
    /// Money in is positive, money out negative, whatever sign the source used.
    /// Transfers keep their sign: they can go either way.
    /// </summary>
    public static decimal NormaliseSign(string type, decimal amount) => type switch
    {
        Withdraw or Fee => -Math.Abs(amount),
        Deposit or InterestOnFreeCash or LendingInterest => Math.Abs(amount),
        _ => amount,
    };
}
