using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class SavedPaymentMethodConfiguration : IEntityTypeConfiguration<SavedPaymentMethod>
{
    public void Configure(EntityTypeBuilder<SavedPaymentMethod> builder)
    {
        builder.Property(c => c.BuyerId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(c => c.PayPalVaultId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(c => c.PayPalCustomerId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(c => c.Brand).HasMaxLength(32);
        builder.Property(c => c.Last4).HasMaxLength(4);
        builder.Property(c => c.Expiry).HasMaxLength(7);
        builder.Property(c => c.Label).HasMaxLength(100);
    }
}
