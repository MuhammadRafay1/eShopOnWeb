using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class EnrolmentConfiguration : IEntityTypeConfiguration<Enrolment>
{
    public void Configure(EntityTypeBuilder<Enrolment> builder)
    {
        builder.Property(e => e.BuyerId).IsRequired().HasMaxLength(256);
        builder.HasIndex(e => e.BuyerId).IsUnique();
        builder.Property(e => e.TaxCountry).HasMaxLength(2);
        builder.Property(e => e.TaxId).HasMaxLength(64);
        builder.Property(e => e.UpvestUserId).HasMaxLength(64);
        builder.Property(e => e.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(e => e.UpvestAccountId).HasMaxLength(64);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
    }
}
