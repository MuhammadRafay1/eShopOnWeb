using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.ShopperId).IsRequired().HasMaxLength(256);
        builder.HasIndex(i => i.ShopperId).IsUnique();
        builder.Property(i => i.UpvestUserId).IsRequired().HasMaxLength(64);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(64);
        builder.Property(i => i.SelectedModelId).HasMaxLength(128);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.StatusDetail).HasMaxLength(512);
    }
}
