using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorRecordConfiguration : IEntityTypeConfiguration<InvestorRecord>
{
    public void Configure(EntityTypeBuilder<InvestorRecord> builder)
    {
        builder.Property(i => i.ShopperUserName)
            .IsRequired()
            .HasMaxLength(256);

        // One investor record per shopper: the natural key the duplicate-sign-up claim is refused on.
        builder.HasIndex(i => i.ShopperUserName).IsUnique();

        builder.Property(i => i.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(i => i.SelectedModelId)
            .HasMaxLength(128);
    }
}
