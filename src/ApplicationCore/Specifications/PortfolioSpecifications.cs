using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByShopUserIdSpec : Specification<Investor>
{
    public InvestorByShopUserIdSpec(string shopUserId)
    {
        Query.Where(i => i.ShopUserId == shopUserId);
    }
}

public sealed class ModelPortfolioByModelIdSpec : Specification<ModelPortfolio>
{
    public ModelPortfolioByModelIdSpec(string modelId)
    {
        Query.Where(m => m.ModelId == modelId)
            .Include(m => m.Funds);
    }
}

public sealed class AllModelPortfoliosSpec : Specification<ModelPortfolio>
{
    public AllModelPortfoliosSpec()
    {
        Query.Include(m => m.Funds);
    }
}

public sealed class PaymentsByShopUserIdSpec : Specification<Payment>
{
    public PaymentsByShopUserIdSpec(string shopUserId)
    {
        Query.Where(p => p.ShopUserId == shopUserId)
            .OrderByDescending(p => p.CreatedAt);
    }
}

public sealed class RealignmentByIdSpec : Specification<Realignment>
{
    public RealignmentByIdSpec(System.Guid realignmentId)
    {
        Query.Where(r => r.RealignmentId == realignmentId);
    }
}
