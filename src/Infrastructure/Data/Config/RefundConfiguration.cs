using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.Property(r => r.PayPalRefundId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(r => r.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(108);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(r => r.Amount).HasColumnType("decimal(18,2)");

        // Makes the idempotent-refund lookup a single indexed query and prevents a double refund
        // under the same caller-supplied key from being persisted twice.
        builder.HasIndex(r => new { r.PaymentId, r.IdempotencyKey }).IsUnique();
    }
}
