using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentOperationClaimAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PaymentOperationClaimConfiguration : IEntityTypeConfiguration<PaymentOperationClaim>
{
    public void Configure(EntityTypeBuilder<PaymentOperationClaim> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasMaxLength(160).ValueGeneratedNever();
        builder.Property(c => c.Outcome).HasMaxLength(256);
    }
}
