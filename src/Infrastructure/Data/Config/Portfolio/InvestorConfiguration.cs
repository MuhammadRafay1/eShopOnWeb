using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data.Config.Portfolio;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.Property(i => i.BuyerId).IsRequired().HasMaxLength(256);
        // One investor record per shopper: the store refuses a second claim for the same BuyerId.
        builder.HasIndex(i => i.BuyerId).IsUnique();

        builder.Property(i => i.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(i => i.LastName).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Email).IsRequired().HasMaxLength(256);
        builder.Property(i => i.Nationality).IsRequired().HasMaxLength(2);
        builder.Property(i => i.AddressLine1).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Postcode).IsRequired().HasMaxLength(20);
        builder.Property(i => i.City).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Country).IsRequired().HasMaxLength(2);
        builder.Property(i => i.TaxId).IsRequired().HasMaxLength(64);
        builder.Property(i => i.TaxCountry).IsRequired().HasMaxLength(2);
        builder.Property(i => i.BirthCity).HasMaxLength(100);
        builder.Property(i => i.BirthCountry).HasMaxLength(2);
        builder.Property(i => i.PhoneNumber).HasMaxLength(20);

        builder.Property(i => i.IdentityDocumentType).IsRequired().HasMaxLength(50);
        builder.Property(i => i.IdentityDocumentNumber).IsRequired().HasMaxLength(100);
        builder.Property(i => i.IdentityDocumentIssuingCountry).IsRequired().HasMaxLength(2);

        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
    }
}
