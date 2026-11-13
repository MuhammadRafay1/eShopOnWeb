using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        var navigation = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(m => m.ModelId).IsRequired();
        builder.HasIndex(m => m.ModelId).IsUnique();

        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.UpvestAllocationId).HasMaxLength(100);

        builder.OwnsMany(m => m.Funds, fund =>
        {
            fund.WithOwner();
            fund.Property(f => f.InstrumentId).IsRequired().HasMaxLength(20);
            fund.Property(f => f.Weight).HasColumnType("decimal(9,4)");
        });
    }
}
