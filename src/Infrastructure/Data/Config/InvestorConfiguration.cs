using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.BuyerId)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(i => i.BuyerId).IsUnique();

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.PendingAmount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(i => i.UpvestUserId).HasMaxLength(100);
        builder.Property(i => i.UpvestAccountGroupId).HasMaxLength(100);
        builder.Property(i => i.UpvestAccountId).HasMaxLength(100);
    }
}
