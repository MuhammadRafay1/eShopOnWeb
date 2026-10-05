using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config;

public class SpareChangeLedgerEntryConfiguration : IEntityTypeConfiguration<SpareChangeLedgerEntry>
{
    public void Configure(EntityTypeBuilder<SpareChangeLedgerEntry> builder)
    {
        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)");
    }
}
