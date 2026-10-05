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

        builder.Property(i => i.BuyerId)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(i => i.BuyerId).IsUnique();
        builder.HasIndex(i => i.EnrolmentId).IsUnique();

        builder.Property(i => i.EnrolmentId).IsRequired();
        builder.Property(i => i.Status).IsRequired();
        builder.Property(i => i.SetAsideCents).IsRequired();

        builder.Property(i => i.UpvestUserId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(64);
    }
}
