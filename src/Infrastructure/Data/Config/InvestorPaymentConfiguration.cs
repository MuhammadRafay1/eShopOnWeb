using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorPaymentConfiguration : IEntityTypeConfiguration<InvestorPayment>
{
    public void Configure(EntityTypeBuilder<InvestorPayment> builder)
    {
        builder.Property(p => p.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(p => p.ShopperId);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.UpvestPortfolioOrderId).HasMaxLength(100);
        builder.Property(p => p.UpvestTopupId).HasMaxLength(100);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.FailureReason).HasMaxLength(500);
    }
}
