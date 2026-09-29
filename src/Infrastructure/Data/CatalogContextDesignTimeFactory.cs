using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Microsoft.eShopWeb.Infrastructure.Data;

/// <summary>
/// Design-time factory so EF Core tooling (migrations) can construct a CatalogContext against the
/// SQL Server provider without booting the whole web host. Only ever used by the `dotnet ef` tool.
/// The connection string is a design-time placeholder - `migrations add` does not connect.
/// </summary>
public class CatalogContextDesignTimeFactory : IDesignTimeDbContextFactory<CatalogContext>
{
    public CatalogContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Integrated Security=true;Initial Catalog=Microsoft.eShopOnWeb.CatalogDb;");
        return new CatalogContext(optionsBuilder.Options);
    }
}
