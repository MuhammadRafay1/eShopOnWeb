using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config.Portfolio;

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.Property(m => m.ModelId).IsRequired().HasMaxLength(100);
        // One model per operator-chosen id.
        builder.HasIndex(m => m.ModelId).IsUnique();

        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);

        var navigation = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.Funds)
            .WithOne()
            .HasForeignKey(f => f.ModelPortfolioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModelFundConfiguration : IEntityTypeConfiguration<ModelFund>
{
    public void Configure(EntityTypeBuilder<ModelFund> builder)
    {
        builder.Property(f => f.InstrumentId).IsRequired().HasMaxLength(12);
        builder.Property(f => f.Weight).HasPrecision(8, 5);
    }
}
