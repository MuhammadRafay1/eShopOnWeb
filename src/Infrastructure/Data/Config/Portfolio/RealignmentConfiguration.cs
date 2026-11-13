using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config.Portfolio;

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.HasIndex(r => r.RealignmentId).IsUnique();

        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
    }
}
