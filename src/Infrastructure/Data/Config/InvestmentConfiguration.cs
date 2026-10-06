using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.Property(i => i.PublicId).IsRequired();
        builder.HasIndex(i => i.PublicId).IsUnique();

        builder.Property(i => i.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.UpvestOrderId).HasMaxLength(100);
    }
}
