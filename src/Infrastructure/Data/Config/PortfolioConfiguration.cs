using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(i => i.UpvestUserId).IsRequired().HasMaxLength(64);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(64);
        builder.Property(i => i.SelectedModelId).HasMaxLength(128);
        builder.HasIndex(i => i.BuyerId).IsUnique();
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.Property(m => m.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.UpvestAllocationId).HasMaxLength(64);
        builder.HasIndex(m => m.ModelId).IsUnique();

        var funds = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        funds?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.Funds)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModelFundConfiguration : IEntityTypeConfiguration<ModelFund>
{
    public void Configure(EntityTypeBuilder<ModelFund> builder)
    {
        builder.Property(f => f.InstrumentId).IsRequired().HasMaxLength(12);
        builder.Property(f => f.Weight).HasPrecision(7, 4);
    }
}

public class PortfolioPaymentConfiguration : IEntityTypeConfiguration<PortfolioPayment>
{
    public void Configure(EntityTypeBuilder<PortfolioPayment> builder)
    {
        builder.Property(p => p.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Status).IsRequired().HasMaxLength(20);
        builder.Property(p => p.UpvestTopupId).HasMaxLength(64);
        builder.Property(p => p.UpvestOrderId).HasMaxLength(64);
        builder.HasIndex(p => p.PublicId).IsUnique();
        builder.HasIndex(p => p.BuyerId);
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(20);
        builder.Property(r => r.UpvestExecutionId).HasMaxLength(64);
        builder.HasIndex(r => r.PublicId).IsUnique();
    }
}
