using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class RoundUpConfiguration : IEntityTypeConfiguration<RoundUp>
{
    public void Configure(EntityTypeBuilder<RoundUp> builder)
    {
        builder.Property(r => r.BuyerId)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(r => new { r.BuyerId, r.OrderId }).IsUnique();
    }
}
