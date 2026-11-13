using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorRecordConfiguration : IEntityTypeConfiguration<InvestorRecord>
{
    public void Configure(EntityTypeBuilder<InvestorRecord> builder)
    {
        // Keyed by the shopper's identity so a duplicate sign-up is refused by the primary key.
        builder.HasKey(i => i.BuyerId);
        builder.Property(i => i.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(i => i.SignUpStatus).IsRequired().HasMaxLength(20);
        builder.Property(i => i.SelectedModelId).HasMaxLength(128);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        builder.HasKey(m => m.ModelId);
        builder.Property(m => m.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);

        var funds = builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
        funds?.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(m => m.Funds).WithOne().OnDelete(DeleteBehavior.Cascade);
    }
}

public class ModelFundConfiguration : IEntityTypeConfiguration<ModelFund>
{
    public void Configure(EntityTypeBuilder<ModelFund> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.InstrumentId).IsRequired().HasMaxLength(32);
        builder.Property(f => f.Weight).HasColumnType("decimal(9,5)");
    }
}

public class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> builder)
    {
        builder.HasKey(p => p.PaymentId);
        builder.Property(p => p.PaymentId).IsRequired().HasMaxLength(64);
        builder.Property(p => p.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Status).IsRequired().HasMaxLength(20);
        builder.HasIndex(p => p.BuyerId);
    }
}

public class RealignmentRecordConfiguration : IEntityTypeConfiguration<RealignmentRecord>
{
    public void Configure(EntityTypeBuilder<RealignmentRecord> builder)
    {
        builder.HasKey(r => r.RealignmentId);
        builder.Property(r => r.RealignmentId).IsRequired().HasMaxLength(64);
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(20);
    }
}
