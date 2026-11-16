using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.ShopUserName).IsRequired().HasMaxLength(256);
        builder.HasIndex(i => i.ShopUserName).IsUnique();
        builder.Property(i => i.UpvestUserId).HasMaxLength(64);
        builder.Property(i => i.UpvestUserStatus).HasMaxLength(32);
        builder.Property(i => i.AccountGroupId).HasMaxLength(64);
        builder.Property(i => i.AccountGroupStatus).HasMaxLength(32);
        builder.Property(i => i.AccountId).HasMaxLength(64);
        builder.Property(i => i.AccountStatus).HasMaxLength(32);
        builder.Property(i => i.ModelId).HasMaxLength(128);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.Property(m => m.ModelId).IsRequired().HasMaxLength(128);
        builder.HasIndex(m => m.ModelId).IsUnique();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.UpvestAllocationId).HasMaxLength(64);

        builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(m => m.Funds, f =>
        {
            f.WithOwner();
            f.Property(x => x.InstrumentId).IsRequired().HasMaxLength(12);
            f.Property(x => x.Weight).HasColumnType("decimal(8,5)");
        });
    }
}

public class PortfolioPaymentConfiguration : IEntityTypeConfiguration<PortfolioPayment>
{
    public void Configure(EntityTypeBuilder<PortfolioPayment> builder)
    {
        builder.Property(p => p.ShopUserName).IsRequired().HasMaxLength(256);
        builder.HasIndex(p => p.ShopUserName);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.UpvestTopupId).HasMaxLength(64);
        builder.Property(p => p.TopupStatus).HasMaxLength(32);
        builder.Property(p => p.UpvestOrderId).HasMaxLength(64);
        builder.Property(p => p.OrderStatus).HasMaxLength(32);
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(r => r.UpvestExecutionId).HasMaxLength(64);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
    }
}
