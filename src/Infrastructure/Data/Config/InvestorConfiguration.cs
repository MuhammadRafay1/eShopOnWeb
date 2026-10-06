using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.PublicId).IsRequired();
        builder.HasIndex(i => i.PublicId).IsUnique();

        builder.Property(i => i.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(i => i.ShopperId).IsUnique();

        builder.Property(i => i.UpvestUserId).IsRequired().HasMaxLength(100);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(100);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(100);
        builder.Property(i => i.UpvestKycCheckId).HasMaxLength(100);

        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.PendingAmount).HasColumnType("decimal(18,2)");

        var investments = builder.Metadata.FindNavigation(nameof(Investor.Investments));
        investments?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(i => i.Investments)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
