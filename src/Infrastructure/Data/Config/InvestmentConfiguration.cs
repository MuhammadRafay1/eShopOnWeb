using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.Property(i => i.PublicId).IsRequired();
        builder.HasIndex(i => i.PublicId).IsUnique();

        builder.Property(i => i.Amount).HasColumnType("decimal(18,2)");
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.ProviderOrderId).IsRequired().HasMaxLength(64);
        builder.Property(i => i.CreatedAt).IsRequired();
    }
}
