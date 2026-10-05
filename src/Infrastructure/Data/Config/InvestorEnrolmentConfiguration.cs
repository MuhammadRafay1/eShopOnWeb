using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorEnrolmentConfiguration : IEntityTypeConfiguration<InvestorEnrolment>
{
    public void Configure(EntityTypeBuilder<InvestorEnrolment> builder)
    {
        builder.Property(e => e.BuyerId)
            .IsRequired()
            .HasMaxLength(256);

        // One enrolment per shopper: a unique key the store enforces (a primary-key-style guarantee).
        builder.HasIndex(e => e.BuyerId).IsUnique();

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.TaxId).HasMaxLength(64);
        builder.Property(e => e.TaxCountry).HasMaxLength(2);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.SetAsideBalance)
            .HasColumnType("decimal(18,2)");
    }
}
