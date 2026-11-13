using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.HasKey(m => m.ModelId);
        builder.Property(m => m.ModelId).HasMaxLength(256).ValueGeneratedNever();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(256);
        builder.Property(m => m.CreateAllocationIdempotencyKey).IsRequired();

        var funds = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        funds?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(m => m.Funds, fb =>
        {
            fb.Property(f => f.InstrumentId).IsRequired().HasMaxLength(12);
            fb.Property(f => f.Weight).HasPrecision(18, 5);
        });
    }
}
