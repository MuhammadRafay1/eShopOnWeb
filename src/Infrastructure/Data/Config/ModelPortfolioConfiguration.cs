using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.Property(m => m.ModelId)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(m => m.ModelId).IsUnique();

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(256);

        var funds = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        funds?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(m => m.Funds, f =>
        {
            f.WithOwner();
            f.Property(x => x.InstrumentId)
                .IsRequired()
                .HasMaxLength(12); // ISIN
            f.Property(x => x.Weight)
                .HasColumnType("decimal(8,5)");
        });
    }
}
