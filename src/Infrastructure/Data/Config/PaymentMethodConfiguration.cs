using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.Property(pm => pm.BuyerId).HasMaxLength(256).IsRequired();
        builder.Property(pm => pm.PayPalVaultId).HasMaxLength(64).IsRequired();
        builder.Property(pm => pm.PayPalCustomerId).HasMaxLength(64);
        builder.Property(pm => pm.CardBrand).HasMaxLength(32);
        builder.Property(pm => pm.Last4).HasMaxLength(4).IsRequired();
        builder.Property(pm => pm.Expiry).HasMaxLength(7);

        builder.HasIndex(pm => pm.BuyerId);
    }
}
