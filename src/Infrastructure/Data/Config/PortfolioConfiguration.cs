using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        // Natural key = shopper identity. A primary-key insert is refused even by the in-memory
        // provider, so a double sign-up is rejected rather than racing through a read-then-write.
        builder.HasKey(i => i.ShopUserId);
        builder.Property(i => i.ShopUserId).HasMaxLength(256);
        builder.Ignore(i => i.Id);
        builder.Ignore(i => i.HasAccount);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.HasKey(m => m.ModelId);
        builder.Property(m => m.ModelId).HasMaxLength(128);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(256);
        builder.Ignore(m => m.Id);

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
        builder.HasKey(f => f.Id);
        builder.Property(f => f.InstrumentId).IsRequired().HasMaxLength(32);
        builder.Property(f => f.Weight).HasPrecision(6, 2);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.PaymentId);
        builder.Property(p => p.ShopUserId).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.LocalOutcome).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(p => p.Id);
        builder.HasIndex(p => p.ShopUserId);
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.HasKey(r => r.RealignmentId);
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(r => r.LocalOutcome).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(r => r.Id);
    }
}
