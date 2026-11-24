using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(i => i.ShopperId).IsUnique();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(p => p.ShopperId);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.Property(m => m.ModelId).IsRequired().HasMaxLength(128);
        builder.HasIndex(m => m.ModelId).IsUnique();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);

        var fundsNavigation = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        fundsNavigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(m => m.Funds, f =>
        {
            f.WithOwner();
            f.Property(x => x.InstrumentId).IsRequired().HasMaxLength(12);
            f.Property(x => x.Weight).HasColumnType("decimal(9,6)");
        });
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
    }
}
