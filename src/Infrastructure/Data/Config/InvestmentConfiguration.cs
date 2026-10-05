using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.Property(i => i.InvestmentId).IsRequired();
        builder.HasIndex(i => i.InvestmentId).IsUnique();

        builder.Property(i => i.InvestorId).IsRequired();
        builder.HasIndex(i => i.InvestorId);

        builder.Property(i => i.Amount).HasColumnType("decimal(18,2)");
        builder.Property(i => i.Status).IsRequired();
        builder.Property(i => i.UpvestOrderId).HasMaxLength(64);
    }
}
