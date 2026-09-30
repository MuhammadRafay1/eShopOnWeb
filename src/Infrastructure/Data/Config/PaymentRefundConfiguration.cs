using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentRefundConfiguration : IEntityTypeConfiguration<PaymentRefund>
{
    public void Configure(EntityTypeBuilder<PaymentRefund> builder)
    {
        // DB-level backstop for "repeating a request under the same key must not refund twice":
        // even a race between two same-key requests hits this unique constraint on SaveChanges.
        builder.HasIndex(r => new { r.PaymentId, r.IdempotencyKey }).IsUnique();

        builder.Property(r => r.PayPalRefundId).IsRequired().HasMaxLength(64);
        builder.Property(r => r.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(r => r.Status).HasMaxLength(32);
        builder.Property(r => r.Amount).HasColumnType("decimal(18,2)");
    }
}
