using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorEnrolmentConfiguration : IEntityTypeConfiguration<InvestorEnrolment>
{
    public void Configure(EntityTypeBuilder<InvestorEnrolment> builder)
    {
        builder.Property(e => e.ShopperId).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.UpvestUserId).HasMaxLength(64);
        builder.Property(e => e.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(e => e.UpvestAccountId).HasMaxLength(64);
        builder.HasIndex(e => e.ShopperId).IsUnique();
    }
}

public class SpareChangeLedgerConfiguration : IEntityTypeConfiguration<SpareChangeLedger>
{
    public void Configure(EntityTypeBuilder<SpareChangeLedger> builder)
    {
        builder.Property(l => l.ShopperId).IsRequired().HasMaxLength(256);
        builder.Property(l => l.PendingAmount).HasColumnType("decimal(18,2)");
        builder.HasIndex(l => l.ShopperId).IsUnique();
    }
}

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.Property(i => i.ShopperId).IsRequired().HasMaxLength(256);
        builder.Property(i => i.Amount).HasColumnType("decimal(18,2)");
        builder.Property(i => i.Status).HasConversion<int>();
        builder.Property(i => i.UpvestOrderId).HasMaxLength(64);
        builder.HasIndex(i => i.ShopperId);
    }
}
