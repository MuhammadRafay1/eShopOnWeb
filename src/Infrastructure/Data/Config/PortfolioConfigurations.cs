using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        // Natural key: the shopper's username. A second concurrent sign-up for the same shopper is rejected
        // by this primary key (enforced even by the in-memory provider), which is the duplicate-claim guard.
        builder.HasKey(i => i.ShopperUserName);
        builder.Property(i => i.ShopperUserName).HasMaxLength(256);
        builder.Property(i => i.VirtualBankAccountIban).HasMaxLength(34);
        builder.Property(i => i.ChosenModelId).HasMaxLength(100);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.HasKey(m => m.ModelId);
        builder.Property(m => m.ModelId).HasMaxLength(100);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);

        var navigation = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.Funds)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModelPortfolioFundConfiguration : IEntityTypeConfiguration<ModelPortfolioFund>
{
    public void Configure(EntityTypeBuilder<ModelPortfolioFund> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.InstrumentId).IsRequired().HasMaxLength(12);
        builder.Property(f => f.Weight).HasColumnType("decimal(9,5)");
    }
}

public class PortfolioPaymentConfiguration : IEntityTypeConfiguration<PortfolioPayment>
{
    public void Configure(EntityTypeBuilder<PortfolioPayment> builder)
    {
        builder.HasKey(p => p.PaymentId);
        builder.Property(p => p.ShopperUserName).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.HasIndex(p => p.ShopperUserName);
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.HasKey(r => r.RealignmentId);
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(100);
    }
}
