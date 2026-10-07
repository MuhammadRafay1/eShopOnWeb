using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        var investments = builder.Metadata.FindNavigation(nameof(Investor.Investments));
        investments?.SetPropertyAccessMode(PropertyAccessMode.Field);

        var ledger = builder.Metadata.FindNavigation(nameof(Investor.Ledger));
        ledger?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(i => i.PublicId).IsRequired();
        builder.HasIndex(i => i.PublicId).IsUnique();

        builder.Property(i => i.BuyerId)
            .IsRequired()
            .HasMaxLength(256);
        builder.HasIndex(i => i.BuyerId).IsUnique();

        builder.Property(i => i.UpvestUserId).HasMaxLength(100);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(100);

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(i => i.PendingAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.HasMany(i => i.Investments)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Ledger)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.Property(i => i.PublicId).IsRequired();
        builder.HasIndex(i => i.PublicId).IsUnique();

        builder.Property(i => i.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.InstrumentId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(i => i.UpvestOrderId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}

public class RoundUpEntryConfiguration : IEntityTypeConfiguration<RoundUpEntry>
{
    public void Configure(EntityTypeBuilder<RoundUpEntry> builder)
    {
        builder.Property(r => r.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");
    }
}
