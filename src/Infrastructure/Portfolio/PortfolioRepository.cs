using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

namespace Microsoft.eShopWeb.Infrastructure.Portfolio;

/// <summary>EF-backed repository over <see cref="PortfolioContext"/>.</summary>
public class PortfolioRepository<T> : RepositoryBase<T>, IPortfolioRepository<T> where T : class, IAggregateRoot
{
    public PortfolioRepository(PortfolioContext dbContext) : base(dbContext)
    {
    }
}
