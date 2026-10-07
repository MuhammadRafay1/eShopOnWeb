using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.eShopWeb.Infrastructure.Investing;

/// <summary>
/// Storage for the investing feature, kept separate from <c>CatalogContext</c> so
/// the capability is fully additive. Under the in-memory provider this lives only
/// for the lifetime of the host, which is acceptable for this environment.
/// </summary>
public class InvestingContext : DbContext
{
    public InvestingContext(DbContextOptions<InvestingContext> options) : base(options) { }

    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<RoundUpEntry> RoundUpEntries => Set<RoundUpEntry>();
    public DbSet<Investment> Investments => Set<Investment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Enrolment>(e =>
        {
            e.Property(x => x.BuyerId).IsRequired();
            e.HasIndex(x => x.BuyerId).IsUnique();
        });

        builder.Entity<RoundUpEntry>(e =>
        {
            e.Property(x => x.BuyerId).IsRequired();
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.BuyerId);
        });

        builder.Entity<Investment>(e =>
        {
            e.Property(x => x.BuyerId).IsRequired();
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.HasIndex(x => x.BuyerId);
        });
    }
}
