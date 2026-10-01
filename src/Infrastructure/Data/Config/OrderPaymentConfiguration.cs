using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class OrderPaymentConfiguration : IEntityTypeConfiguration<OrderPayment>
{
    public void Configure(EntityTypeBuilder<OrderPayment> builder)
    {
        builder.HasKey(p => p.OrderId);
        // OrderId is a caller-assigned key (the Order's own id), never store-generated.
        builder.Property(p => p.OrderId).ValueGeneratedNever();

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(p => p.InvoiceId).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CapturedGross).HasColumnType("decimal(18,2)");
        builder.Property(p => p.PayPalFee).HasColumnType("decimal(18,2)");
        builder.Property(p => p.NetAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.RefundedTotal).HasColumnType("decimal(18,2)");

        builder.Property(p => p.AuthorizationId).HasMaxLength(64);
        builder.Property(p => p.PayPalOrderId).HasMaxLength(64);
        builder.Property(p => p.CaptureId).HasMaxLength(64);

        // Optimistic-concurrency token guarding every state transition against a racing duplicate request.
        builder.Property(p => p.Version).IsConcurrencyToken();
    }
}
