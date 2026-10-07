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

        builder.Property(i => i.EnrolmentId).IsRequired();

        builder.Property(i => i.PendingAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<int>();
    }
}
