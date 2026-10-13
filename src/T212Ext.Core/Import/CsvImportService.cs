using System.Security.Cryptography;
using System.Text;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Import;

public sealed class CsvImportResult
{
    public int DividendsAdded { get; set; }

    /// <summary>Dividends already known (from the API or an earlier import); their withholding tax was filled in.</summary>
    public int DividendsMatched { get; set; }

    public int TransactionsAdded { get; set; }
    public int TransactionsMatched { get; set; }

    /// <summary>Buys and sells: not imported yet (see docs/BACKLOG.md).</summary>
    public int OrdersSkipped { get; set; }

    /// <summary>Other actions (card payments, currency conversions…) by name, with their count.</summary>
    public Dictionary<string, int> OtherSkipped { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<T212CsvError> Errors { get; } = [];
}

/// <summary>
/// Imports a Trading 212 CSV report into an account: dividends (with the withholding tax the API
/// does not give) and cash movements. Importing the same file twice changes nothing.
/// </summary>
public sealed class CsvImportService(IDbContextFactory<T212ExtDbContext> dbFactory, TimeProvider time)
{
    private static readonly Dictionary<string, string> CashActions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Deposit"] = CashTransactionTypes.Deposit,
        ["Withdrawal"] = CashTransactionTypes.Withdraw,
        ["Interest on cash"] = CashTransactionTypes.InterestOnFreeCash,
        ["Lending interest"] = CashTransactionTypes.LendingInterest,
        ["Dividend adjustment"] = CashTransactionTypes.DividendAdjustment,
    };

    public async Task<CsvImportResult> ImportAsync(int accountId, Stream csv, CancellationToken cancellationToken = default)
    {
        T212CsvParseResult parsed;
        using (var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            parsed = T212CsvParser.Parse(reader);

        var result = new CsvImportResult();
        result.Errors.AddRange(parsed.Errors);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Account {accountId} does not exist.");

        var dividendRows = new List<T212CsvRow>();
        var cashRows = new List<(T212CsvRow Row, string Type)>();

        foreach (var row in parsed.Rows)
        {
            // Cash first: "Dividend adjustment" is a cash correction, not a dividend of an instrument.
            if (CashActions.TryGetValue(row.Action, out var type))
                cashRows.Add((row, type));
            else if (row.Action.StartsWith("Dividend", StringComparison.OrdinalIgnoreCase))
                dividendRows.Add(row);
            else if (row.Action.EndsWith(" buy", StringComparison.OrdinalIgnoreCase) || row.Action.EndsWith(" sell", StringComparison.OrdinalIgnoreCase))
                result.OrdersSkipped++;
            else
                result.OtherSkipped[row.Action] = result.OtherSkipped.GetValueOrDefault(row.Action) + 1;
        }

        await ImportDividendsAsync(db, account, dividendRows, result, cancellationToken);
        await ImportCashAsync(db, account, cashRows, result, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task ImportDividendsAsync(T212ExtDbContext db, BrokerAccount account, List<T212CsvRow> rows, CsvImportResult result, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;

        var from = rows.Min(r => r.Time) - DividendMatcher.DateTolerance;
        var to = rows.Max(r => r.Time) + DividendMatcher.DateTolerance;
        var existing = await db.Dividends
            .Where(d => d.AccountId == account.Id && d.PaidOn >= from && d.PaidOn <= to)
            .ToListAsync(cancellationToken);

        var isins = rows.Select(r => r.Isin).OfType<string>().Distinct().ToList();
        var instruments = await db.Instruments
            .Where(i => i.Isin != null && isins.Contains(i.Isin))
            .ToListAsync(cancellationToken);

        // Each stored dividend absorbs one CSV row at most: in this file (matched), and across
        // files (CsvReference). Only rows no CSV row has claimed are matched by their values.
        var matched = new HashSet<Dividend>(ReferenceEqualityComparer.Instance);
        var now = time.GetUtcNow();

        foreach (var row in rows)
        {
            if (row.Total is null)
            {
                result.Errors.Add(new T212CsvError(row.LineNumber, "Dividend without a total."));
                continue;
            }

            var reference = row.Id ?? SyntheticReference(row);
            var quantity = row.Shares ?? 0;
            var amount = row.Total.Value;

            var dividend = existing.FirstOrDefault(d => (d.CsvReference == reference || d.Reference == reference) && !matched.Contains(d))
                ?? DividendMatcher.FindMatch(existing.Where(d => d.CsvReference is null && !matched.Contains(d)), row.Isin, row.Ticker, row.Time, quantity, amount);

            if (dividend is null)
            {
                dividend = new Dividend
                {
                    AccountId = account.Id,
                    Reference = reference,
                    CsvReference = reference,
                    Sources = DataSources.Csv,
                    Ticker = ResolveTicker(instruments, row),
                    Isin = row.Isin,
                    InstrumentName = row.Name,
                    Type = DividendType(row.Action),
                    PaidOn = row.Time,
                    Quantity = quantity,
                    GrossAmountPerShare = row.PricePerShare,
                    InstrumentCurrency = row.PriceCurrency,
                    Amount = amount,
                    Currency = row.TotalCurrency ?? account.Currency ?? "",
                    AmountInEuro = row.TotalCurrency == "EUR" ? amount : null,
                    ImportedAt = now,
                };
                db.Dividends.Add(dividend);
                existing.Add(dividend);
                result.DividendsAdded++;
            }
            else
            {
                dividend.Sources |= DataSources.Csv;
                dividend.CsvReference = reference;
                result.DividendsMatched++;
            }

            matched.Add(dividend);

            if (row.WithholdingTax is { } tax)
            {
                dividend.WithholdingTax = Math.Abs(tax);
                dividend.WithholdingTaxCurrency = row.WithholdingTaxCurrency ?? row.PriceCurrency;
            }
        }
    }

    private async Task ImportCashAsync(T212ExtDbContext db, BrokerAccount account, List<(T212CsvRow Row, string Type)> rows, CsvImportResult result, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;

        var from = rows.Min(r => r.Row.Time) - CashTransactionMatcher.DateTolerance;
        var to = rows.Max(r => r.Row.Time) + CashTransactionMatcher.DateTolerance;
        var existing = await db.CashTransactions
            .Where(t => t.AccountId == account.Id && t.DateTime >= from && t.DateTime <= to)
            .ToListAsync(cancellationToken);

        // As with dividends: one CSV row per movement, and value matching only for unclaimed rows.
        var matched = new HashSet<CashTransaction>(ReferenceEqualityComparer.Instance);
        var now = time.GetUtcNow();

        foreach (var (row, type) in rows)
        {
            if (row.Total is null)
            {
                result.Errors.Add(new T212CsvError(row.LineNumber, $"{row.Action} without a total."));
                continue;
            }

            var reference = row.Id ?? SyntheticReference(row);
            var amount = CashTransactionTypes.NormaliseSign(type, row.Total.Value);

            var transaction = existing.FirstOrDefault(t => (t.CsvReference == reference || t.Reference == reference) && !matched.Contains(t))
                ?? CashTransactionMatcher.FindMatch(existing.Where(t => t.CsvReference is null && !matched.Contains(t)), type, amount, row.Time);

            if (transaction is null)
            {
                transaction = new CashTransaction
                {
                    AccountId = account.Id,
                    Reference = reference,
                    CsvReference = reference,
                    Sources = DataSources.Csv,
                    Type = type,
                    Amount = amount,
                    Currency = row.TotalCurrency ?? account.Currency ?? "",
                    DateTime = row.Time,
                    ImportedAt = now,
                };
                db.CashTransactions.Add(transaction);
                existing.Add(transaction);
                result.TransactionsAdded++;
            }
            else
            {
                transaction.Sources |= DataSources.Csv;
                transaction.CsvReference = reference;
                result.TransactionsMatched++;
            }

            matched.Add(transaction);
        }
    }

    /// <summary>"Dividend (Ordinary)" → "Ordinary".</summary>
    internal static string DividendType(string action)
    {
        var open = action.IndexOf('(');
        var close = action.LastIndexOf(')');
        return open >= 0 && close > open ? action[(open + 1)..close].Trim() : "Dividend";
    }

    /// <summary>Prefers Trading 212's ticker (found by ISIN and currency) over the report's plain symbol.</summary>
    private static string ResolveTicker(List<Instrument> instruments, T212CsvRow row)
    {
        var candidates = instruments.Where(i => i.Isin == row.Isin).ToList();
        var instrument = candidates.FirstOrDefault(i => i.Currency == row.PriceCurrency) ?? candidates.FirstOrDefault();
        return instrument?.Ticker ?? row.Ticker ?? row.Isin ?? "?";
    }

    /// <summary>A stable reference for rows without an ID, so a second import finds the first one's rows.</summary>
    internal static string SyntheticReference(T212CsvRow row)
    {
        var key = FormattableString.Invariant($"{row.Action}|{row.Time:O}|{row.Isin}|{row.Shares}|{row.Total}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return "csv:" + Convert.ToHexStringLower(hash)[..24];
    }
}
