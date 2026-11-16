using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.ShopperId).IsRequired().HasMaxLength(256);
        // One investor record per shopper. Enforced by a real database; the application also guards
        // with a lookup before the claim, because the in-memory provider ignores unique indexes.
        builder.HasIndex(i => i.ShopperId).IsUnique();
        builder.Property(i => i.StatusCache).IsRequired().HasMaxLength(32);
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

        var navigation = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(m => m.Funds).WithOne().OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModelFundConfiguration : IEntityTypeConfiguration<ModelFund>
{
    public void Configure(EntityTypeBuilder<ModelFund> builder)
    {
        builder.Property(f => f.InstrumentId).IsRequired().HasMaxLength(32);
        builder.Property(f => f.Weight).HasColumnType("decimal(9,4)");
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasIndex(p => p.PaymentId).IsUnique();
        builder.Property(p => p.ShopperId).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.ClientReference).IsRequired().HasMaxLength(64);
        builder.Property(p => p.StatusCache).IsRequired().HasMaxLength(32);
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.HasIndex(r => r.RealignmentId).IsUnique();
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(r => r.StatusCache).IsRequired().HasMaxLength(32);
    }
}
