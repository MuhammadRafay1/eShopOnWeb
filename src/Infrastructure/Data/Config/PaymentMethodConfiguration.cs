using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PaymentMethods");

        // PayPal's own vault id is the primary key — it is also the shopper-facing paymentMethodId.
        builder.HasKey(p => p.PayPalVaultId);
        builder.Property(p => p.PayPalVaultId).HasMaxLength(255).ValueGeneratedNever();

        builder.Property(p => p.BuyerId).HasMaxLength(256).IsRequired();
        builder.Property(p => p.PayPalCustomerId).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Brand).HasMaxLength(50);
        builder.Property(p => p.Last4).HasMaxLength(4);

        builder.HasIndex(p => p.BuyerId);
    }
}
