using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class SavedPaymentMethodConfiguration : IEntityTypeConfiguration<SavedPaymentMethod>
{
    public void Configure(EntityTypeBuilder<SavedPaymentMethod> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasMaxLength(64).ValueGeneratedNever();

        builder.Property(m => m.BuyerId).HasMaxLength(256).IsRequired();
        builder.Property(m => m.Brand).HasMaxLength(32);
        builder.Property(m => m.LastDigits).HasMaxLength(8);
        builder.Property(m => m.Expiry).HasMaxLength(16);
        builder.Property(m => m.CardholderName).HasMaxLength(256);

        builder.HasIndex(m => m.BuyerId);
    }
}
