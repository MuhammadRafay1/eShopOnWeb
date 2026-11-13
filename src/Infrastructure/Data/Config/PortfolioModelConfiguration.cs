using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PortfolioModelConfiguration : IEntityTypeConfiguration<PortfolioModel>
{
    public void Configure(EntityTypeBuilder<PortfolioModel> builder)
    {
        // The operator's model id is the primary key, so a re-PUT of the same id is a create-or-replace.
        builder.HasKey(m => m.ModelId);

        builder.Property(m => m.ModelId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(m => m.Name)
            .HasMaxLength(200)
            .IsRequired();

        var navigation = builder.Metadata.FindNavigation(nameof(PortfolioModel.Funds));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(m => m.Funds, fund =>
        {
            fund.WithOwner();
            fund.Property(f => f.InstrumentId)
                .HasMaxLength(12)
                .IsRequired();
            fund.Property(f => f.Weight)
                .HasColumnType("decimal(9,5)")
                .IsRequired();
        });
    }
}
