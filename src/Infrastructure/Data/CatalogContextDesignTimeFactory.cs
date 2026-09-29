using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Microsoft.eShopWeb.Infrastructure.Data;

/// <summary>
/// Design-time factory used only by EF Core tooling (e.g. <c>dotnet ef migrations add</c>). It pins
/// the SQL Server provider so migrations can be scaffolded against the real relational model even
/// though the app runs on the in-memory provider in this environment. The connection string is a
/// non-secret placeholder — it is never opened during scaffolding.
/// </summary>
public class CatalogContextDesignTimeFactory : IDesignTimeDbContextFactory<CatalogContext>
{
    public CatalogContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Microsoft.eShopOnWeb.CatalogDb;Trusted_Connection=True;");
        return new CatalogContext(optionsBuilder.Options);
    }
}
