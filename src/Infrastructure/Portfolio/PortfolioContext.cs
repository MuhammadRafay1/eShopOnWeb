using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>
/// The application's own store for the managed-portfolio capability: the mapping from a shopper to the
/// Upvest ids created for them, the operator's model portfolios, payments and realignments. Upvest is the
/// system of record for money and holdings; this context holds only what the app must own to enforce
/// per-shopper isolation, duplicate claims and status reconciliation.
/// </summary>
public class PortfolioContext : DbContext
{
    public PortfolioContext(DbContextOptions<PortfolioContext> options) : base(options) { }

    public DbSet<Investor> Investors => Set<Investor>();
    public DbSet<ModelPortfolio> Models => Set<ModelPortfolio>();
    public DbSet<ModelFund> ModelFunds => Set<ModelFund>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Realignment> Realignments => Set<Realignment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Investor>(b =>
        {
            b.HasKey(i => i.ShopperId);
            b.Property(i => i.ShopperId).HasMaxLength(256);
        });

        builder.Entity<ModelPortfolio>(b =>
        {
            b.HasKey(m => m.ModelId);
            b.Property(m => m.ModelId).HasMaxLength(128);
            b.Property(m => m.Name).HasMaxLength(256);
            b.HasMany(m => m.Funds)
                .WithOne()
                .HasForeignKey(f => f.ModelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ModelFund>(b =>
        {
            b.HasKey(f => f.Id);
            b.Property(f => f.InstrumentId).HasMaxLength(32);
            b.Property(f => f.Weight).HasPrecision(8, 5);
        });

        builder.Entity<Payment>(b =>
        {
            b.HasKey(p => p.PaymentId);
            b.Property(p => p.ShopperId).HasMaxLength(256);
            b.Property(p => p.Amount).HasPrecision(18, 2);
            b.HasIndex(p => p.ShopperId);
        });

        builder.Entity<Realignment>(b =>
        {
            b.HasKey(r => r.RealignmentId);
            b.Property(r => r.ModelId).HasMaxLength(128);
        });
    }
}
