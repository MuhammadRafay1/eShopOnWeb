using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class SpareChangeEntryConfiguration : IEntityTypeConfiguration<SpareChangeEntry>
{
    public void Configure(EntityTypeBuilder<SpareChangeEntry> builder)
    {
        builder.Property(e => e.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(e => e.OrderTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");
    }
}
