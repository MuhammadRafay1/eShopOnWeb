using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.ShopperUserName).IsRequired().HasMaxLength(256);
        // One investor per shopper — the uniqueness that refuses a duplicate sign-up (SQL Server).
        builder.HasIndex(i => i.ShopperUserName).IsUnique();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        var navigation = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(m => m.ModelKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(m => m.ModelKey).IsUnique();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.HasMany(m => m.Funds).WithOne().OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModelFundConfiguration : IEntityTypeConfiguration<ModelFund>
{
    public void Configure(EntityTypeBuilder<ModelFund> builder)
    {
        builder.Property(f => f.InstrumentId).IsRequired().HasMaxLength(12);
        builder.Property(f => f.Weight).HasColumnType("decimal(9,5)");
    }
}

public class PortfolioPaymentConfiguration : IEntityTypeConfiguration<PortfolioPayment>
{
    public void Configure(EntityTypeBuilder<PortfolioPayment> builder)
    {
        builder.Property(p => p.ShopperUserName).IsRequired().HasMaxLength(256);
        builder.HasIndex(p => p.PaymentKey).IsUnique();
        builder.HasIndex(p => p.ShopperUserName);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
    }
}

public class PortfolioRealignmentConfiguration : IEntityTypeConfiguration<PortfolioRealignment>
{
    public void Configure(EntityTypeBuilder<PortfolioRealignment> builder)
    {
        builder.Property(r => r.ModelKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(r => r.RealignmentKey).IsUnique();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
    }
}
