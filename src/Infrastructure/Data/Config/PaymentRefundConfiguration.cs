using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentRefundConfiguration : IEntityTypeConfiguration<PaymentRefund>
{
    public void Configure(EntityTypeBuilder<PaymentRefund> builder)
    {
        builder.HasKey(r => r.IdempotencyKey);
        builder.Property(r => r.IdempotencyKey).HasMaxLength(128).ValueGeneratedNever();

        builder.Property(r => r.CaptureId).HasMaxLength(64).IsRequired();
        builder.Property(r => r.PayPalRefundId).HasMaxLength(64);
        builder.Property(r => r.Status).HasMaxLength(32).IsRequired();
        builder.Property(r => r.Amount).HasColumnType("decimal(18,2)");

        builder.HasIndex(r => r.OrderId);
    }
}
