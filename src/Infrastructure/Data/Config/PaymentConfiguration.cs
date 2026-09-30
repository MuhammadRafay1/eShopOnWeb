using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        // 1:1 with Order enforced at the DB level even without a navigation property.
        builder.HasIndex(p => p.OrderId).IsUnique();

        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.PayPalOrderId).IsRequired().HasMaxLength(64);
        builder.Property(p => p.AuthorizationId).IsRequired().HasMaxLength(64);
        builder.Property(p => p.AuthorizationStatus).HasMaxLength(32);
        builder.Property(p => p.CaptureId).HasMaxLength(64);
        builder.Property(p => p.CaptureStatus).HasMaxLength(32);

        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CapturedAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.PayPalFee).HasColumnType("decimal(18,2)");
        builder.Property(p => p.NetAmount).HasColumnType("decimal(18,2)");

        // Child refunds reached only through Payment.Refunds — same field-access pattern
        // OrderConfiguration uses for OrderItems.
        builder.HasMany(p => p.Refunds)
            .WithOne()
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        var navigation = builder.Metadata.FindNavigation(nameof(Payment.Refunds));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
