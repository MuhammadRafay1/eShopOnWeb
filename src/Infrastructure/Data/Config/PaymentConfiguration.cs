using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(p => p.ShopperId);
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(p => p.UpvestTopupId).HasMaxLength(64);
        builder.Property(p => p.UpvestPortfolioOrderId).HasMaxLength(64);
        builder.Property(p => p.StatusDetail).HasMaxLength(512);
    }
}
