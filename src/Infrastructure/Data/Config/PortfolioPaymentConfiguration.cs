using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PortfolioPaymentConfiguration : IEntityTypeConfiguration<PortfolioPayment>
{
    public void Configure(EntityTypeBuilder<PortfolioPayment> builder)
    {
        // Keyed by the application-generated PaymentId, created before the Upvest calls so a resend
        // of the same submission conflicts on the primary key.
        builder.HasKey(p => p.PaymentId);
        builder.Property(p => p.PaymentId).ValueGeneratedNever();

        builder.Property(p => p.BuyerId).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Status).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasIndex(p => p.BuyerId);
    }
}
