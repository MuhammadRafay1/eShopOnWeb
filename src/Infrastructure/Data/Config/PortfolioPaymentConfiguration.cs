using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PortfolioPaymentConfiguration : IEntityTypeConfiguration<PortfolioPayment>
{
    public void Configure(EntityTypeBuilder<PortfolioPayment> builder)
    {
        builder.HasIndex(p => p.PaymentId).IsUnique();

        builder.Property(p => p.ShopperUserName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(20);
    }
}
