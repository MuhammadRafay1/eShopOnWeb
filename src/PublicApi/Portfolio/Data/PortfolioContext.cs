using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.PublicApi.Portfolio.Domain;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.Data;

/// <summary>
/// PublicApi-owned store for the managed-investment-portfolio records (investors, models, payments,
/// realignments). Kept separate from the catalog/identity contexts so PublicApi's investment state is
/// isolated, as the deployment requires.
/// </summary>
public class PortfolioContext : DbContext
{
    public PortfolioContext(DbContextOptions<PortfolioContext> options) : base(options)
    {
    }

    public DbSet<InvestorRecord> Investors => Set<InvestorRecord>();
    public DbSet<ModelPortfolio> Models => Set<ModelPortfolio>();
    public DbSet<ModelFund> ModelFunds => Set<ModelFund>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<RealignmentRecord> Realignments => Set<RealignmentRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<InvestorRecord>(b =>
        {
            b.HasKey(x => x.ShopperId);
            b.Property(x => x.ShopperId).HasMaxLength(256);
        });

        builder.Entity<ModelPortfolio>(b =>
        {
            b.HasKey(x => x.ModelId);
            b.Property(x => x.ModelId).HasMaxLength(128);
            b.Property(x => x.Name).HasMaxLength(256);
            b.HasMany(x => x.Funds).WithOne().HasForeignKey(f => f.ModelId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ModelFund>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.InstrumentId).HasMaxLength(12);
            b.Property(x => x.Weight).HasPrecision(8, 5);
        });

        builder.Entity<PaymentRecord>(b =>
        {
            b.HasKey(x => x.PaymentId);
            b.Property(x => x.ShopperId).HasMaxLength(256);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.HasIndex(x => x.ShopperId);
        });

        builder.Entity<RealignmentRecord>(b =>
        {
            b.HasKey(x => x.RealignmentId);
            b.Property(x => x.ModelId).HasMaxLength(128);
            b.HasIndex(x => x.ModelId);
        });
    }
}
