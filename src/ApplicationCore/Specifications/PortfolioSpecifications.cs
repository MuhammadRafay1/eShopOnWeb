using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByShopperSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByShopperSpecification(string shopperId)
    {
        Query.Where(i => i.ShopperId == shopperId);
    }
}

public sealed class PaymentsByShopperSpecification : Specification<Payment>
{
    public PaymentsByShopperSpecification(string shopperId)
    {
        Query.Where(p => p.ShopperId == shopperId)
             .OrderByDescending(p => p.CreatedAt)
             .ThenByDescending(p => p.Id);
    }
}

public sealed class ModelPortfolioByModelIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByModelIdSpecification(string modelId)
    {
        Query.Where(m => m.ModelId == modelId);
    }
}
