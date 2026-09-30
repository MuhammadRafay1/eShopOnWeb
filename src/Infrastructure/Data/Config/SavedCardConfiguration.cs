using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class SavedCardConfiguration : IEntityTypeConfiguration<SavedCard>
{
    public void Configure(EntityTypeBuilder<SavedCard> builder)
    {
        builder.HasIndex(c => c.BuyerId);
        builder.HasIndex(c => new { c.BuyerId, c.PayPalVaultId }).IsUnique();

        builder.Property(c => c.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(c => c.PayPalVaultId).IsRequired().HasMaxLength(64);
        builder.Property(c => c.Brand).IsRequired().HasMaxLength(40);
        builder.Property(c => c.LastFourDigits).IsRequired().HasMaxLength(4);
        builder.Property(c => c.Expiry).IsRequired().HasMaxLength(7);
        builder.Property(c => c.CardholderName).HasMaxLength(200);
    }
}
