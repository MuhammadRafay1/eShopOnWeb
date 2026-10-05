using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class SubscriptionEnrollmentConfiguration : IEntityTypeConfiguration<SubscriptionEnrollment>
{
    public void Configure(EntityTypeBuilder<SubscriptionEnrollment> builder)
    {
        // The primary key is the claim: one enrollment per buyer and plan, so a double-submitted subscribe is
        // refused by the store before it can reach the billing system.
        builder.HasKey(e => new { e.BuyerId, e.PlanHandle });

        builder.Property(e => e.BuyerId)
            .HasMaxLength(256);

        builder.Property(e => e.PlanHandle)
            .HasMaxLength(128);

        builder.Property(e => e.SubscriptionReference)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(e => e.SubscriptionReference)
            .IsUnique();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(16);
    }
}
