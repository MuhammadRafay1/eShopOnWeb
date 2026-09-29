using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.Property(pm => pm.Alias).HasMaxLength(256);

        // CardId holds the PayPal vault token id — never the actual PAN.
        builder.Property(pm => pm.CardId).HasMaxLength(255);
        builder.Property(pm => pm.Brand).HasMaxLength(30);
        builder.Property(pm => pm.Last4).HasMaxLength(4);
        builder.Property(pm => pm.ExpiryYearMonth).HasMaxLength(7);
    }
}
