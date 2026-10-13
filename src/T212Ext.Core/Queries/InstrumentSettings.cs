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
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.InstrumentSettings.AsNoTracking().Where(s => s.Isin == isin).Select(s => s.SourceCountry).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Sets the source country (ISO alpha-2); null or the ISIN's own country removes the override.</summary>
    public async Task SetSourceCountryAsync(string isin, string? country, CancellationToken cancellationToken = default)
    {
        country = string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant();
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
}
