using System.ComponentModel.DataAnnotations;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Arisoul.T212Ext.Core.Accounts;

/// <summary>The configured accounts, and their rows in the database.</summary>
public sealed class AccountRegistry
{
    private readonly IReadOnlyList<AccountOptions> _accounts;
    private readonly TimeProvider _time;

    public AccountRegistry(IOptions<T212Options> options, TimeProvider time)
    {
        _accounts = options.Value.Accounts;
        _time = time;

        var failures = new List<string>();
        foreach (var account in _accounts)
        {
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(account, new ValidationContext(account), results, validateAllProperties: true))
                failures.AddRange(results.Select(r => $"Account '{account.Key}': {r.ErrorMessage}"));
        }

        failures.AddRange(_accounts
            .GroupBy(a => a.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"Account key '{g.Key}' is configured more than once."));

        if (failures.Count > 0)
            throw new OptionsValidationException(T212Options.Section, typeof(T212Options), failures);
    }

    public IReadOnlyList<AccountOptions> Configured => _accounts;

    public AccountOptions? Find(string key) => _accounts.FirstOrDefault(a => a.Key == key);

    /// <summary>Creates the rows of new accounts and refreshes the name and environment of existing ones.</summary>
    public async Task EnsureAccountsAsync(T212ExtDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.Accounts.ToDictionaryAsync(a => a.Key, StringComparer.Ordinal, cancellationToken);

        foreach (var options in _accounts)
        {
            if (existing.TryGetValue(options.Key, out var account))
            {
                account.Name = options.Name;
                account.Environment = options.Environment.ToString();
                continue;
            }

            db.Accounts.Add(new BrokerAccount
            {
                Key = options.Key,
                Name = options.Name,
                Environment = options.Environment.ToString(),
                CreatedAt = _time.GetUtcNow(),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
