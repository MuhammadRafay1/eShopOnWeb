using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class PortfolioRealignmentConfiguration : IEntityTypeConfiguration<PortfolioRealignment>
{
    public void Configure(EntityTypeBuilder<PortfolioRealignment> builder)
    {
        builder.HasKey(r => r.RealignmentId);
        builder.Property(r => r.RealignmentId).ValueGeneratedNever();

        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(256);
        builder.Property(r => r.Status).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
    }
}
