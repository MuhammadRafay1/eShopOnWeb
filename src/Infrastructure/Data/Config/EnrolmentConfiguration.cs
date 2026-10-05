using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class EnrolmentConfiguration : IEntityTypeConfiguration<Enrolment>
{
    public void Configure(EntityTypeBuilder<Enrolment> builder)
    {
        builder.Property(e => e.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(e => e.ShopperId).IsUnique();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PendingAmount).HasPrecision(18, 2);
    }
}
