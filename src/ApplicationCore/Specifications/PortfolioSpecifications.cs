using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByShopUserSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByShopUserSpecification(string shopUserName)
    {
        Query.Where(i => i.ShopUserName == shopUserName);
    }
}

public sealed class ModelPortfolioByModelIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByModelIdSpecification(string modelId)
    {
        Query.Where(m => m.ModelId == modelId);
    }
}

public sealed class PaymentsByShopUserSpecification : Specification<PortfolioPayment>
{
    public PaymentsByShopUserSpecification(string shopUserName)
    {
        // Newest first.
        Query.Where(p => p.ShopUserName == shopUserName)
             .OrderByDescending(p => p.CreatedAt);
    }
}
