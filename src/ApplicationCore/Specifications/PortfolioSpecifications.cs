using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public class InvestorByShopperSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByShopperSpecification(string shopperId)
    {
        Query.Where(i => i.ShopperId == shopperId);
    }
}

public class InvestorsByModelSpecification : Specification<Investor>
{
    public InvestorsByModelSpecification(string modelId)
    {
        Query.Where(i => i.FollowedModelId == modelId);
    }
}

public class ModelPortfolioByModelIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByModelIdSpecification(string modelId)
    {
        Query.Where(m => m.ModelId == modelId)
            .Include(m => m.Funds);
    }
}

public class ModelPortfoliosWithFundsSpecification : Specification<ModelPortfolio>
{
    public ModelPortfoliosWithFundsSpecification()
    {
        Query.Include(m => m.Funds)
            .OrderBy(m => m.ModelId);
    }
}

public class PaymentsByShopperSpecification : Specification<PortfolioPayment>
{
    public PaymentsByShopperSpecification(string shopperId)
    {
        Query.Where(p => p.ShopperId == shopperId)
            .OrderByDescending(p => p.CreatedAt);
    }
}

public class RealignmentByPublicIdSpecification : Specification<Realignment>, ISingleResultSpecification<Realignment>
{
    public RealignmentByPublicIdSpecification(string publicId)
    {
        Query.Where(r => r.PublicId == publicId);
    }
}
