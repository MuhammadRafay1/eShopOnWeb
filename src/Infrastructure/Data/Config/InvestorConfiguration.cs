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

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.PendingAmountCents).IsRequired();
        builder.Property(i => i.InvestedAmountCents).IsRequired();

        builder.HasMany(i => i.Investments)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
