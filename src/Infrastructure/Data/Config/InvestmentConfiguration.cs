using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.ShopperId).HasMaxLength(256).IsRequired();
        builder.Property(i => i.Stage).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(i => i.ShopperId);

        builder.Ignore(i => i.Status);
        builder.Ignore(i => i.IsTerminal);
    }
}
