using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        // Not unique — one buyer can save many cards.
        builder.HasIndex(m => m.BuyerId);

        builder.Property(m => m.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(m => m.PayPalVaultId).IsRequired().HasMaxLength(128);
        builder.Property(m => m.Brand).HasMaxLength(32);
        builder.Property(m => m.LastDigits).HasMaxLength(4);
        builder.Property(m => m.Expiry).HasMaxLength(7);
    }
}
