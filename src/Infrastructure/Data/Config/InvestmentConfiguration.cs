using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.Property(i => i.BuyerId)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(i => i.BuyerId);

        builder.Property(i => i.InvestmentId).IsRequired();

        builder.Property(i => i.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<int>();
    }
}
