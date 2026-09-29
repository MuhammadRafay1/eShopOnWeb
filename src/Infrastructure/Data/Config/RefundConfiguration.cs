using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        // The database-enforced guarantee behind "repeating a request under the same key must not
        // refund twice": two refund rows for one payment can never share an idempotency key.
        builder.HasIndex(r => new { r.PaymentId, r.IdempotencyKey }).IsUnique();

        builder.Property(r => r.PayPalRefundId).IsRequired().HasMaxLength(64);
        builder.Property(r => r.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(32);
        builder.Property(r => r.Amount).HasColumnType("decimal(18,2)");
    }
}
