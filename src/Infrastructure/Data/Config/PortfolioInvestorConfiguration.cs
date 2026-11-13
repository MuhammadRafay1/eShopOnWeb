using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PortfolioInvestorConfiguration : IEntityTypeConfiguration<PortfolioInvestor>
{
    public void Configure(EntityTypeBuilder<PortfolioInvestor> builder)
    {
        // Keyed by the shopper's identity: one investor per shopper, and the primary-key insert is
        // what rejects a concurrent duplicate sign-up.
        builder.HasKey(i => i.BuyerId);
        builder.Property(i => i.BuyerId).HasMaxLength(256).ValueGeneratedNever();

        builder.Property(i => i.InvestorId).IsRequired();
        builder.Property(i => i.Status).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.SelectedModelId).HasMaxLength(256);
    }
}
