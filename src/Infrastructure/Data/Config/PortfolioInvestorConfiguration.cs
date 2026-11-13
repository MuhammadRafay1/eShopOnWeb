using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PortfolioInvestorConfiguration : IEntityTypeConfiguration<PortfolioInvestor>
{
    public void Configure(EntityTypeBuilder<PortfolioInvestor> builder)
    {
        // The shopper identity is the primary key: a second onboarding of the same shopper is refused by the
        // store (enforced even by the in-memory provider), which is the duplicate-claim guard.
        builder.HasKey(i => i.ShopperKey);

        builder.Property(i => i.ShopperKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.SelectedModelId)
            .HasMaxLength(128);

        builder.Property(i => i.LastKnownStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
    }
}
