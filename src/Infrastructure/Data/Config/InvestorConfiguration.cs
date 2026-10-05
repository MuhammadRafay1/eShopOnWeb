using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.EnrolmentId).IsRequired();
        builder.HasIndex(i => i.EnrolmentId).IsUnique();

        builder.Property(i => i.BuyerId).IsRequired().HasMaxLength(256);
        builder.HasIndex(i => i.BuyerId).IsUnique();

        builder.Property(i => i.Status).IsRequired();

        builder.Property(i => i.UpvestUserId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(64);

        builder.Property(i => i.PendingAmount).HasColumnType("decimal(18,2)");
    }
}
