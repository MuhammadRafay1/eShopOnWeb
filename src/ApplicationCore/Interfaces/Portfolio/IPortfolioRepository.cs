using Ardalis.Specification;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;

/// <summary>
/// Repository over the application's own managed-portfolio store (investors, models, payments,
/// realignments). Separate from <see cref="IRepository{T}"/>, which is bound to the catalog store.
/// </summary>
public interface IPortfolioRepository<T> : IRepositoryBase<T> where T : class, IAggregateRoot
{
}
