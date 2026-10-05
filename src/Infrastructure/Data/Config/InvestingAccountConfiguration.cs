using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestingAccountConfiguration : IEntityTypeConfiguration<InvestingAccount>
{
    public void Configure(EntityTypeBuilder<InvestingAccount> builder)
    {
        builder.Metadata.FindNavigation(nameof(InvestingAccount.Investments))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(InvestingAccount.SpareChangeEntries))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(a => a.BuyerId)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(a => a.BuyerId).IsUnique();

        builder.HasIndex(a => a.EnrolmentId).IsUnique();

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(a => a.PendingAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(a => a.UpvestUserId).HasMaxLength(64);
        builder.Property(a => a.UpvestAccountGroupId).HasMaxLength(64);
        builder.Property(a => a.UpvestAccountId).HasMaxLength(64);
    }
}
