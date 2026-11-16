using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A single model portfolio by its app id, with its funds loaded.</summary>
public sealed class ModelPortfolioByIdSpec : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByIdSpec(string modelId)
    {
        Query.Where(m => m.ModelId == modelId).Include(m => m.Funds);
    }
}

/// <summary>Every model portfolio, with funds loaded.</summary>
public sealed class ModelPortfoliosWithFundsSpec : Specification<ModelPortfolio>
{
    public ModelPortfoliosWithFundsSpec()
    {
        Query.Include(m => m.Funds).OrderBy(m => m.ModelId);
    }
}

/// <summary>A shopper's payments, newest first.</summary>
public sealed class PaymentsByShopperSpec : Specification<PortfolioPayment>
{
    public PaymentsByShopperSpec(string shopperUserName)
    {
        Query.Where(p => p.ShopperUserName == shopperUserName)
             .OrderByDescending(p => p.CreatedAt);
    }
}
