using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class EnrolmentConfiguration : IEntityTypeConfiguration<Enrolment>
{
    public void Configure(EntityTypeBuilder<Enrolment> builder)
    {
        // Key = the shopper's identity, so the store refuses a duplicate enrolment for the same shopper.
        builder.HasKey(e => e.ShopperId);
        builder.Property(e => e.ShopperId).HasMaxLength(256).ValueGeneratedNever();
        builder.Property(e => e.Stage).HasConversion<string>().HasMaxLength(40);

        // Computed projections are not persisted.
        builder.Ignore(e => e.Status);
        builder.Ignore(e => e.IsAccepted);
    }
}
