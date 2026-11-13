using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class RealignmentConfiguration : IEntityTypeConfiguration<Realignment>
{
    public void Configure(EntityTypeBuilder<Realignment> builder)
    {
        builder.Property(r => r.ModelId).IsRequired().HasMaxLength(128);
        builder.HasIndex(r => r.ModelId);
        builder.Property(r => r.UpvestExecutionId).HasMaxLength(64);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.StatusDetail).HasMaxLength(512);
    }
}
