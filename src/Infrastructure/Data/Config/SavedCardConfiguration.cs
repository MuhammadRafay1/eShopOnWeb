using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class SavedCardConfiguration : IEntityTypeConfiguration<SavedCard>
{
    public void Configure(EntityTypeBuilder<SavedCard> builder)
    {
        builder.Property(c => c.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(c => c.PayPalVaultId).IsRequired().HasMaxLength(64);
        builder.Property(c => c.PayPalCustomerId).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Brand).IsRequired().HasMaxLength(32);
        builder.Property(c => c.Last4).IsRequired().HasMaxLength(8);
        builder.Property(c => c.Expiry).IsRequired().HasMaxLength(8);
    }
}
