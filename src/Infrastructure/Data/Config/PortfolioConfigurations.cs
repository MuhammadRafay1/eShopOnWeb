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
        builder.Property(i => i.FirstName).IsRequired().HasMaxLength(128);
        builder.Property(i => i.LastName).IsRequired().HasMaxLength(128);
        builder.Property(i => i.UpvestUserId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(i => i.SecuritiesAccountNumber).HasMaxLength(32);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(64);
        builder.Property(i => i.FollowedModelId).HasMaxLength(128);
        builder.Property(i => i.FollowedAllocationId).HasMaxLength(64);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        var navigation = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(m => m.ModelId).IsRequired().HasMaxLength(128);
        builder.HasIndex(m => m.ModelId).IsUnique();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(256);
        builder.Property(m => m.UpvestAllocationId).HasMaxLength(64);

        builder.HasMany(m => m.Funds)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModelPortfolioFundConfiguration : IEntityTypeConfiguration<ModelPortfolioFund>
{
    public void Configure(EntityTypeBuilder<ModelPortfolioFund> builder)
    {
        builder.Property(f => f.InstrumentId).IsRequired().HasMaxLength(12);
        builder.Property(f => f.Weight).HasPrecision(9, 4);
    }
}

public class PortfolioPaymentConfiguration : IEntityTypeConfiguration<PortfolioPayment>
{
    public void Configure(EntityTypeBuilder<PortfolioPayment> builder)
    {
        builder.Property(p => p.PublicId).IsRequired().HasMaxLength(64);
        builder.HasIndex(p => p.PublicId).IsUnique();
        builder.Property(p => p.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(p => p.ShopperId);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.UpvestBankTransactionId).HasMaxLength(64);
        builder.Property(p => p.UpvestOrderId).HasMaxLength(64);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.Property(r => r.PublicId).IsRequired().HasMaxLength(64);
        builder.HasIndex(r => r.PublicId).IsUnique();
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(r => r.UpvestExecutionId).HasMaxLength(64);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
    }
}
