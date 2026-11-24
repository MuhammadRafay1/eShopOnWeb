using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.BuyerId).IsRequired().HasMaxLength(256);
        builder.HasIndex(i => i.BuyerId).IsUnique();
        builder.Property(i => i.SelectedModelId).HasMaxLength(128);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.Property(m => m.ModelId).IsRequired().HasMaxLength(128);
        builder.HasIndex(m => m.ModelId).IsUnique();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(256);

        builder.OwnsMany(m => m.Funds, fund =>
        {
            fund.Property(f => f.InstrumentId).IsRequired().HasMaxLength(12);
            fund.Property(f => f.Weight).HasPrecision(9, 4);
        });
        builder.Navigation(m => m.Funds).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class PortfolioPaymentConfiguration : IEntityTypeConfiguration<PortfolioPayment>
{
    public void Configure(EntityTypeBuilder<PortfolioPayment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.HasIndex(p => p.PublicId).IsUnique();
        builder.HasIndex(p => p.InvestorId);
    }
}

public class PortfolioRealignmentConfiguration : IEntityTypeConfiguration<PortfolioRealignment>
{
    public void Configure(EntityTypeBuilder<PortfolioRealignment> builder)
    {
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.HasIndex(r => r.PublicId).IsUnique();
    }
}
