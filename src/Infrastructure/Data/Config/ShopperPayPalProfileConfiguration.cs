using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class ShopperPayPalProfileConfiguration : IEntityTypeConfiguration<ShopperPayPalProfile>
{
    public void Configure(EntityTypeBuilder<ShopperPayPalProfile> builder)
    {
        builder.HasKey(p => p.BuyerId);
        builder.Property(p => p.BuyerId).HasMaxLength(256).ValueGeneratedNever();
        builder.Property(p => p.PayPalCustomerId).HasMaxLength(64).IsRequired();
    }
}
