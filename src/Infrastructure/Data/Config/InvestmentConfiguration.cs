using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.Property(i => i.BuyerId).IsRequired().HasMaxLength(256);
        builder.HasIndex(i => i.BuyerId);
        builder.Property(i => i.ClientReference).HasMaxLength(64);
        builder.Property(i => i.UpvestOrderId).HasMaxLength(64);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
    }
}
