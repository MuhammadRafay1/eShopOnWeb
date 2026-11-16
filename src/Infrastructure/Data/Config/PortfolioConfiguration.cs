using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Ignore(i => i.CanPayIn);

        builder.Property(i => i.PublicId).IsRequired();
        builder.HasIndex(i => i.PublicId).IsUnique();

        builder.Property(i => i.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(i => i.ShopperId).IsUnique();

        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.UpvestUserId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(64);
    }
}

public class ModelPortfolioConfiguration : IEntityTypeConfiguration<ModelPortfolio>
{
    public void Configure(EntityTypeBuilder<ModelPortfolio> builder)
    {
        // The model id is supplied by the operator (route value), not generated.
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.UpvestAllocationId).HasMaxLength(64);

        builder.HasMany(m => m.Funds)
            .WithOne()
            .HasForeignKey(f => f.ModelPortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(ModelPortfolio.Funds))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ModelFundConfiguration : IEntityTypeConfiguration<ModelFund>
{
    public void Configure(EntityTypeBuilder<ModelFund> builder)
    {
        builder.Property(f => f.InstrumentId).IsRequired().HasMaxLength(12);
        builder.Property(f => f.Weight).HasColumnType("decimal(9,4)");
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.PublicId).IsRequired();
        builder.HasIndex(p => p.PublicId).IsUnique();

        builder.Property(p => p.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(p => p.ShopperId);

        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.UpvestOrderId).HasMaxLength(64);
    }
}

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.Property(r => r.PublicId).IsRequired();
        builder.HasIndex(r => r.PublicId).IsUnique();

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.UpvestExecutionId).HasMaxLength(64);
    }
}
