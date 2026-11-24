using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.ShopperUserName).IsRequired().HasMaxLength(256);
        builder.HasIndex(i => i.ShopperUserName).IsUnique();
        builder.Property(i => i.UpvestUserId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(64);
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
        builder.Property(m => m.UpvestAllocationId).HasMaxLength(64);

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

public class PortfolioPaymentConfiguration : IEntityTypeConfiguration<PortfolioPayment>
{
    public void Configure(EntityTypeBuilder<PortfolioPayment> builder)
    {
        builder.Property(p => p.PublicId).IsRequired();
        builder.HasIndex(p => p.PublicId).IsUnique();
        builder.Property(p => p.ShopperUserName).IsRequired().HasMaxLength(256);
        builder.HasIndex(p => p.ShopperUserName);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.UpvestCashIncreaseId).HasMaxLength(64);
        builder.Property(p => p.UpvestOrderId).HasMaxLength(64);
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.Property(r => r.PublicId).IsRequired();
        builder.HasIndex(r => r.PublicId).IsUnique();
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(r => r.UpvestExecutionId).HasMaxLength(64);
    }
}
