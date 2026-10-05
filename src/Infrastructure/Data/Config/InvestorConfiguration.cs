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

        // One investor per shopper — the primary guard against a second enrolment for the same shopper.
        builder.HasIndex(i => i.ShopperId).IsUnique();

        builder.Property(i => i.Status)
            .HasConversion<int>();

        builder.HasMany(i => i.Investments)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
