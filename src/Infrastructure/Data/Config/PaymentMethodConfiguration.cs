using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        // Non-unique: one buyer can save many cards.
        builder.HasIndex(pm => pm.BuyerId);

        builder.Property(pm => pm.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(pm => pm.PayPalVaultId).IsRequired().HasMaxLength(64);
        builder.Property(pm => pm.CardBrand).HasMaxLength(32);
        builder.Property(pm => pm.Last4Digits).HasMaxLength(4);
        builder.Property(pm => pm.Expiry).HasMaxLength(7);
    }
}
