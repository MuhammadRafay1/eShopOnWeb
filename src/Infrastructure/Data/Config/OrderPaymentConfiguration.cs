using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class OrderPaymentConfiguration : IEntityTypeConfiguration<OrderPayment>
{
    public void Configure(EntityTypeBuilder<OrderPayment> builder)
    {
        // Private-field navigation for the child refunds collection (same pattern as Order.OrderItems).
        builder.Metadata.FindNavigation(nameof(OrderPayment.Refunds))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // 1:1 with Order, enforced at the DB level; also the race guard for concurrent first-time pays.
        builder.HasIndex(p => p.OrderId).IsUnique();

        builder.Property(p => p.OrderId).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.PayPalOrderId).HasMaxLength(64).IsRequired();
        builder.Property(p => p.PayPalAuthorizationId).HasMaxLength(64).IsRequired();
        builder.Property(p => p.PayPalCaptureId).HasMaxLength(64);

        builder.Property(p => p.AuthorizedAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CapturedAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.PayPalFeeAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.NetAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.TotalRefundedAmount).HasColumnType("decimal(18,2)");

        builder.HasMany(p => p.Refunds)
            .WithOne()
            .HasForeignKey(r => r.OrderPaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
