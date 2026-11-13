using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Returns every model portfolio with its funds, ordered by creation.</summary>
public class ModelPortfoliosAllSpecification : Specification<ModelPortfolio>
{
    public ModelPortfoliosAllSpecification()
    {
        Query.Include(m => m.Funds)
             .OrderBy(m => m.CreatedAt);
    }
}
