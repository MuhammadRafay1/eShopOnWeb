using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.Infrastructure.Data;

/// <summary>
/// Persists the application's own managed-portfolio state (investors, model portfolios, payments,
/// realignments). Kept separate from the catalog/identity contexts so the capability is additive.
/// Primary keys are declared explicitly so a duplicate claim fails at insert rather than overwriting.
/// </summary>
public class PortfolioContext : DbContext
{
    public PortfolioContext(DbContextOptions<PortfolioContext> options) : base(options)
    {
    }

    public DbSet<InvestorRecord> Investors => Set<InvestorRecord>();
    public DbSet<ModelPortfolioRecord> Models => Set<ModelPortfolioRecord>();
    public DbSet<ModelFund> ModelFunds => Set<ModelFund>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<RealignmentRecord> Realignments => Set<RealignmentRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<InvestorRecord>(b =>
        {
            b.HasKey(i => i.Shopper);
            b.Property(i => i.Shopper).HasMaxLength(256);
            b.Property(i => i.ChosenModelId).HasMaxLength(128);
        });

        builder.Entity<ModelPortfolioRecord>(b =>
        {
            b.HasKey(m => m.ModelId);
            b.Property(m => m.ModelId).HasMaxLength(128);
            b.Property(m => m.Name).HasMaxLength(256).IsRequired();
            b.HasMany(m => m.Funds)
                .WithOne()
                .HasForeignKey(f => f.ModelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ModelFund>(b =>
        {
            b.HasKey(f => f.Id);
            b.Property(f => f.InstrumentId).HasMaxLength(64).IsRequired();
            b.Property(f => f.Weight).HasPrecision(9, 4);
            b.Property(f => f.ModelId).HasMaxLength(128).IsRequired();
        });

        builder.Entity<PaymentRecord>(b =>
        {
            b.HasKey(p => p.PaymentId);
            b.Property(p => p.Shopper).HasMaxLength(256).IsRequired();
            b.Property(p => p.Amount).HasPrecision(18, 2);
            b.HasIndex(p => p.Shopper);
        });

        builder.Entity<RealignmentRecord>(b =>
        {
            b.HasKey(r => r.RealignmentId);
            b.Property(r => r.ModelId).HasMaxLength(128).IsRequired();
        });
    }
}
