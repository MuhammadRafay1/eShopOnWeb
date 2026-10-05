using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.HasIndex(i => i.PublicId).IsUnique();

        builder.Property(i => i.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(i => i.UpvestOrderId).HasMaxLength(64);
    }
}
