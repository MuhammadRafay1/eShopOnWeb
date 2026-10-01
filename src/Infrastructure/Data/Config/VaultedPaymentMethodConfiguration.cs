using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class VaultedPaymentMethodConfiguration : IEntityTypeConfiguration<VaultedPaymentMethod>
{
    public void Configure(EntityTypeBuilder<VaultedPaymentMethod> builder)
    {
        builder.Property(pm => pm.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(pm => pm.PayPalVaultId).IsRequired().HasMaxLength(64);

        builder.HasIndex(pm => pm.BuyerId);
    }
}
