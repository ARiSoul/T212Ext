using Arisoul.T212Ext.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arisoul.T212Ext.Core.Data.Configurations;

/// <summary>Column lengths, shared by the configurations and by code that trims incoming text.</summary>
public static class Lengths
{
    public const int Key = 50;
    public const int Name = 300;
    public const int Ticker = 100;
    public const int Isin = 20;
    public const int Currency = 10;
    public const int Code = 100;
    public const int Reference = 100;
    public const int Error = 2000;
}

internal sealed class BrokerAccountConfiguration : IEntityTypeConfiguration<BrokerAccount>
{
    public void Configure(EntityTypeBuilder<BrokerAccount> builder)
    {
        builder.Property(a => a.Key).HasMaxLength(Lengths.Key);
        builder.Property(a => a.Name).HasMaxLength(Lengths.Name);
        builder.Property(a => a.Environment).HasMaxLength(Lengths.Key);
        builder.Property(a => a.Currency).HasMaxLength(Lengths.Currency);
        builder.HasIndex(a => a.Key).IsUnique();
    }
}

internal sealed class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.HasKey(i => i.Ticker);
        builder.Property(i => i.Ticker).HasMaxLength(Lengths.Ticker);
        builder.Property(i => i.Isin).HasMaxLength(Lengths.Isin);
        builder.Property(i => i.Name).HasMaxLength(Lengths.Name);
        builder.Property(i => i.ShortName).HasMaxLength(Lengths.Ticker);
        builder.Property(i => i.Type).HasMaxLength(Lengths.Code);
        builder.Property(i => i.Currency).HasMaxLength(Lengths.Currency);
        builder.HasIndex(i => i.Isin);
    }
}

internal sealed class DividendConfiguration : IEntityTypeConfiguration<Dividend>
{
    public void Configure(EntityTypeBuilder<Dividend> builder)
    {
        builder.Property(d => d.Reference).HasMaxLength(Lengths.Reference);
        builder.Property(d => d.CsvReference).HasMaxLength(Lengths.Reference);
        builder.Property(d => d.Ticker).HasMaxLength(Lengths.Ticker);
        builder.Property(d => d.Isin).HasMaxLength(Lengths.Isin);
        builder.Property(d => d.InstrumentName).HasMaxLength(Lengths.Name);
        builder.Property(d => d.Type).HasMaxLength(Lengths.Code);
        builder.Property(d => d.InstrumentCurrency).HasMaxLength(Lengths.Currency);
        builder.Property(d => d.Currency).HasMaxLength(Lengths.Currency);
        builder.Property(d => d.WithholdingTaxCurrency).HasMaxLength(Lengths.Currency);
        builder.HasOne(d => d.Account).WithMany().HasForeignKey(d => d.AccountId);
        builder.HasIndex(d => new { d.AccountId, d.Reference }).IsUnique();
        builder.HasIndex(d => new { d.AccountId, d.PaidOn });
        builder.HasIndex(d => new { d.AccountId, d.Isin });
        builder.HasIndex(d => new { d.AccountId, d.CsvReference });
    }
}

internal sealed class HistoricalOrderConfiguration : IEntityTypeConfiguration<HistoricalOrder>
{
    public void Configure(EntityTypeBuilder<HistoricalOrder> builder)
    {
        builder.ToTable("Orders");
        builder.Property(o => o.Ticker).HasMaxLength(Lengths.Ticker);
        builder.Property(o => o.Isin).HasMaxLength(Lengths.Isin);
        builder.Property(o => o.InstrumentName).HasMaxLength(Lengths.Name);
        builder.Property(o => o.InstrumentCurrency).HasMaxLength(Lengths.Currency);
        builder.Property(o => o.WalletCurrency).HasMaxLength(Lengths.Currency);
        builder.Property(o => o.Side).HasMaxLength(Lengths.Code);
        builder.Property(o => o.Type).HasMaxLength(Lengths.Code);
        builder.Property(o => o.Strategy).HasMaxLength(Lengths.Code);
        builder.Property(o => o.Status).HasMaxLength(Lengths.Code);
        builder.Property(o => o.InitiatedFrom).HasMaxLength(Lengths.Code);
        builder.Property(o => o.TimeInForce).HasMaxLength(Lengths.Code);
        builder.Property(o => o.FillType).HasMaxLength(Lengths.Code);
        builder.Property(o => o.TradingMethod).HasMaxLength(Lengths.Code);
        builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
        builder.HasIndex(o => new { o.AccountId, o.OrderId, o.FillId });
        builder.HasIndex(o => new { o.AccountId, o.Ticker });

        builder.OwnsMany(o => o.Taxes, taxes =>
        {
            taxes.ToTable("OrderTaxes");
            taxes.Property(t => t.Name).HasMaxLength(Lengths.Code);
            taxes.Property(t => t.Currency).HasMaxLength(Lengths.Currency);
        });
    }
}

internal sealed class CashTransactionConfiguration : IEntityTypeConfiguration<CashTransaction>
{
    public void Configure(EntityTypeBuilder<CashTransaction> builder)
    {
        builder.Property(t => t.Reference).HasMaxLength(Lengths.Reference);
        builder.Property(t => t.CsvReference).HasMaxLength(Lengths.Reference);
        builder.Property(t => t.Type).HasMaxLength(Lengths.Code);
        builder.Property(t => t.Currency).HasMaxLength(Lengths.Currency);
        builder.HasOne(t => t.Account).WithMany().HasForeignKey(t => t.AccountId);
        builder.HasIndex(t => new { t.AccountId, t.Reference }).IsUnique();
        builder.HasIndex(t => new { t.AccountId, t.DateTime });
        builder.HasIndex(t => new { t.AccountId, t.CsvReference });
    }
}

internal sealed class PortfolioSnapshotConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
{
    public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
    {
        builder.Property(s => s.Currency).HasMaxLength(Lengths.Currency);
        builder.HasOne(s => s.Account).WithMany().HasForeignKey(s => s.AccountId);
        builder.HasIndex(s => new { s.AccountId, s.Date }).IsUnique();

        builder.OwnsMany(s => s.Positions, positions =>
        {
            positions.ToTable("PositionSnapshots");
            positions.Property(p => p.Ticker).HasMaxLength(Lengths.Ticker);
            positions.Property(p => p.Isin).HasMaxLength(Lengths.Isin);
            positions.Property(p => p.Name).HasMaxLength(Lengths.Name);
            positions.Property(p => p.InstrumentCurrency).HasMaxLength(Lengths.Currency);
            positions.Property(p => p.WalletCurrency).HasMaxLength(Lengths.Currency);
        });
    }
}

internal sealed class SyncStateConfiguration : IEntityTypeConfiguration<SyncState>
{
    public void Configure(EntityTypeBuilder<SyncState> builder)
    {
        builder.HasKey(s => new { s.AccountId, s.Job });
        builder.HasOne<BrokerAccount>().WithMany().HasForeignKey(s => s.AccountId);
    }
}

internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.Property(r => r.Error).HasMaxLength(Lengths.Error);
        builder.HasOne(r => r.Account).WithMany().HasForeignKey(r => r.AccountId);
        builder.HasIndex(r => r.StartedAt);
    }
}
