using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");

        builder.Property(r => r.PayPalRefundId).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(50);
        builder.Property(r => r.IdempotencyKey).HasMaxLength(255).IsRequired();
        builder.Property(r => r.Amount).HasColumnType("decimal(18,2)");

        // A caller's idempotency key can only ever produce one refund row per payment; the DB
        // itself enforces this, backing up the application-level idempotency check.
        builder.HasIndex("PaymentId", nameof(Refund.IdempotencyKey)).IsUnique();
    }
}
