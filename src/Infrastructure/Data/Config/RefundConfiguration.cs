using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.Property<int>("OrderPaymentId");
        builder.Property(r => r.Amount).HasColumnType("decimal(18,2)");

        // Repeating a refund request under the same idempotency key for the same capture must never
        // refund twice; two distinct partial refunds (different keys) remain legitimate.
        builder.HasIndex("OrderPaymentId", nameof(Refund.IdempotencyKey)).IsUnique();
    }
}
