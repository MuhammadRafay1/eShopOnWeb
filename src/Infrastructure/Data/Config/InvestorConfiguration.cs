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

        builder.Property(i => i.ProviderUserId).IsRequired().HasMaxLength(64);
        builder.Property(i => i.ProviderAccountId).HasMaxLength(64);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.PendingAmount).HasColumnType("decimal(18,2)");

        var investments = builder.Metadata.FindNavigation(nameof(Investor.Investments))!;
        investments.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
