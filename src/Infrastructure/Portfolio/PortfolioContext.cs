using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// The application's own store for the managed-portfolio capability: investor records, model
/// portfolios, payments and realignments. Separate from the catalog/identity contexts; holds no
/// personal data (that is forwarded to Upvest, never persisted here).
/// </summary>
public class PortfolioContext : DbContext
{
#pragma warning disable CS8618 // Required by Entity Framework
    public PortfolioContext(DbContextOptions<PortfolioContext> options) : base(options) { }
#pragma warning restore CS8618

    public DbSet<Investor> Investors { get; set; }
    public DbSet<ModelPortfolio> ModelPortfolios { get; set; }
    public DbSet<ModelFund> ModelFunds { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Realignment> Realignments { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Investor>(b =>
        {
            b.HasKey(i => i.ShopperId);
            b.Property(i => i.ShopperId).HasMaxLength(256);
            b.HasIndex(i => i.InvestorId).IsUnique();
            b.Property(i => i.ChosenModelId).HasMaxLength(100);
            b.Property(i => i.Note).HasMaxLength(512);
        });

        builder.Entity<ModelPortfolio>(b =>
        {
            b.HasKey(m => m.ModelId);
            b.Property(m => m.ModelId).HasMaxLength(100);
            b.Property(m => m.Name).IsRequired().HasMaxLength(200);

            var funds = b.Metadata.FindNavigation(nameof(ModelPortfolio.Funds));
            funds!.SetPropertyAccessMode(PropertyAccessMode.Field);
            b.HasMany(m => m.Funds).WithOne().OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ModelFund>(b =>
        {
            b.HasKey(f => f.Id);
            b.Property(f => f.InstrumentId).IsRequired().HasMaxLength(20);
            b.Property(f => f.Weight).HasColumnType("decimal(9,5)");
        });

        builder.Entity<Payment>(b =>
        {
            b.HasKey(p => p.PaymentId);
            b.Property(p => p.ShopperId).IsRequired().HasMaxLength(256);
            b.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            b.Property(p => p.Note).HasMaxLength(512);
            b.HasIndex(p => p.ShopperId);
        });

        builder.Entity<Realignment>(b =>
        {
            b.HasKey(r => r.RealignmentId);
            b.Property(r => r.ModelId).IsRequired().HasMaxLength(100);
            b.Property(r => r.Note).HasMaxLength(512);
        });
    }
}
