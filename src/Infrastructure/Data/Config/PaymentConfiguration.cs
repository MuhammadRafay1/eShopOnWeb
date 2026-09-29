using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        var navigation = builder.Metadata.FindNavigation(nameof(Payment.Refunds));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        // One Payment per Order.
        builder.HasIndex(p => p.OrderId).IsUnique();

        builder.Property(p => p.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(p => p.InvoiceId).HasMaxLength(127);
        builder.Property(p => p.PayPalOrderId).HasMaxLength(64);
        builder.Property(p => p.AuthorizationId).HasMaxLength(64);
        builder.Property(p => p.CaptureId).HasMaxLength(64);

        builder.Property(p => p.AuthorizationStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.CaptureStatus).HasConversion<string>().HasMaxLength(30);

        builder.Property(p => p.AuthorizedAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.CapturedGrossAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.PayPalFeeAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.NetAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.RefundedAmount).HasColumnType("decimal(18,2)");

        builder.HasMany(p => p.Refunds)
            .WithOne()
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
