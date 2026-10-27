using Arisoul.T212Ext.Core.Analytics;
using Arisoul.T212Ext.Core.Data;
using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Arisoul.T212Ext.Core.Queries;

/// <summary>Per-instrument settings, such as the source country of its income for the tax report.</summary>
public sealed class InstrumentSettings(IDbContextFactory<T212ExtDbContext> dbFactory, TimeProvider time)
{
    public async Task<string?> GetSourceCountryAsync(string isin, CancellationToken cancellationToken = default)
    {
        isin = Codes.Upper(isin) ?? "";
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.InstrumentSettings.AsNoTracking().Where(s => s.Isin == isin).Select(s => s.SourceCountry).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Sets the source country (ISO alpha-2); null or the ISIN's own country removes the override.</summary>
    public async Task SetSourceCountryAsync(string isin, string? country, CancellationToken cancellationToken = default)
    {
        isin = Codes.Upper(isin) ?? throw new ArgumentException("An ISIN is needed.", nameof(isin));
        country = Codes.Upper(country);
        if (country is not null && (country.Length != 2 || !country.All(char.IsAsciiLetterUpper)))
            throw new ArgumentException("A country is two letters (ISO 3166).", nameof(country));
        if (country == DividendAnalytics.CountryOf(isin))
            country = null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var setting = await db.InstrumentSettings.FindAsync([isin], cancellationToken);
        if (setting is null)
        {
            if (country is null)
                return;
            setting = new InstrumentSetting { Isin = isin };
            db.InstrumentSettings.Add(setting);
        }

        setting.SourceCountry = country;
        setting.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockSplit>> GetSplitsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.StockSplits.AsNoTracking().OrderBy(s => s.Date).ToListAsync(cancellationToken);
    }

    /// <summary>Adds (or replaces) the split of an instrument on a date.</summary>
    public async Task AddSplitAsync(string isin, DateOnly date, decimal ratio, CancellationToken cancellationToken = default)
    {
        if (ratio <= 0 || ratio == 1)
            throw new ArgumentOutOfRangeException(nameof(ratio), "A split ratio is positive and not 1.");
        isin = Codes.Upper(isin) ?? throw new ArgumentException("An ISIN is needed.", nameof(isin));

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var split = await db.StockSplits.FirstOrDefaultAsync(s => s.Isin == isin && s.Date == date, cancellationToken);
        if (split is null)
        {
            split = new StockSplit { Isin = isin, Date = date, CreatedAt = time.GetUtcNow() };
            db.StockSplits.Add(split);
        }

        split.Ratio = ratio;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSplitAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await db.StockSplits.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}
