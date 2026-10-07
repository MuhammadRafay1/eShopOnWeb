using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        var navigation = builder.Metadata.FindNavigation(nameof(Investor.Investments));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(i => i.ShopperId)
            .IsRequired()
            .HasMaxLength(256);
        builder.HasIndex(i => i.ShopperId).IsUnique();

        builder.Property(i => i.PendingAmount).HasColumnType("decimal(18,2)");
        builder.Property(i => i.UpvestUserId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(64);

        builder.HasMany(i => i.Investments)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);

        // Derived, not stored.
        builder.Ignore(i => i.InvestedAmount);
        builder.Ignore(i => i.CanInvest);
        builder.Ignore(i => i.HasAccount);
    }
}
