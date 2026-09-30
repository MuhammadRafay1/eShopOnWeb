using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Microsoft.eShopWeb.Infrastructure.Data;

/// <summary>
/// Design-time factory so `dotnet ef migrations` can build a <see cref="CatalogContext"/> against
/// the SQL Server provider without booting the application host (and its seeding). The connection
/// string is a placeholder — migrations are generated from the model, not by connecting.
/// </summary>
public class CatalogContextDesignTimeFactory : IDesignTimeDbContextFactory<CatalogContext>
{
    public CatalogContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Integrated Security=true;Initial Catalog=Microsoft.eShopOnWeb.CatalogDb;",
            b => b.MigrationsAssembly(typeof(CatalogContext).Assembly.FullName));
        return new CatalogContext(optionsBuilder.Options);
    }
}
