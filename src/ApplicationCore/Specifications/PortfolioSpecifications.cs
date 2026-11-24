using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByUserNameSpec : Specification<Investor>
{
    public InvestorByUserNameSpec(string shopperUserName)
    {
        Query.Where(i => i.ShopperUserName == shopperUserName);
    }
}

public sealed class InvestorsBySelectedModelSpec : Specification<Investor>
{
    public InvestorsBySelectedModelSpec(string modelId)
    {
        Query.Where(i => i.SelectedModelId == modelId && i.UpvestAccountId != null);
    }
}

public sealed class ModelByModelIdSpec : Specification<ModelPortfolio>
{
    public ModelByModelIdSpec(string modelId)
    {
        Query.Where(m => m.ModelId == modelId).Include(m => m.Funds);
    }
}

public sealed class AllModelsSpec : Specification<ModelPortfolio>
{
    public AllModelsSpec()
    {
        Query.Include(m => m.Funds).OrderBy(m => m.ModelId);
    }
}

public sealed class PaymentsByUserNameSpec : Specification<PortfolioPayment>
{
    public PaymentsByUserNameSpec(string shopperUserName)
    {
        Query.Where(p => p.ShopperUserName == shopperUserName).OrderByDescending(p => p.CreatedAt);
    }
}

public sealed class RealignmentByPublicIdSpec : Specification<Realignment>
{
    public RealignmentByPublicIdSpec(Guid publicId)
    {
        Query.Where(r => r.PublicId == publicId);
    }
}
