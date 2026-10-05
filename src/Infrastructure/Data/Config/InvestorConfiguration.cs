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

        builder.Property(i => i.BuyerId)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(i => i.BuyerId).IsUnique();

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.PendingAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.UpvestUserId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(64);

        builder.HasMany(i => i.Investments)
            .WithOne()
            .HasForeignKey(x => x.InvestorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Ledger)
            .WithOne()
            .HasForeignKey(x => x.InvestorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
