using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        // Keyed by the shopper's own identity: a second sign-up for the same shopper is refused by the store.
        builder.HasKey(i => i.BuyerId);
        builder.Property(i => i.BuyerId).HasMaxLength(256).ValueGeneratedNever();
        builder.Property(i => i.OwnerName).IsRequired().HasMaxLength(281);
        builder.Property(i => i.LifecycleStatus).HasConversion<int>();
        builder.Property(i => i.FollowedModelId).HasMaxLength(256);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.HasKey(m => m.ModelId);
        builder.Property(m => m.ModelId).HasMaxLength(256).ValueGeneratedNever();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(256);

        var funds = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds))!;
        funds.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(m => m.Funds, f =>
        {
            f.Property(x => x.InstrumentId).IsRequired().HasMaxLength(12);
            f.Property(x => x.Weight).HasColumnType("decimal(8,5)");
        });
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.PaymentId);
        builder.Property(p => p.PaymentId).ValueGeneratedNever();
        builder.Property(p => p.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Status).HasConversion<int>();
        builder.HasIndex(p => p.BuyerId);
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.HasKey(r => r.RealignmentId);
        builder.Property(r => r.RealignmentId).ValueGeneratedNever();
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(256);
        builder.Property(r => r.Status).HasConversion<int>();
    }
}
