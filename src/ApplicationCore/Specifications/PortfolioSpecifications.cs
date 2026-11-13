using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Finds the single investor record owned by a given shopper.</summary>
public sealed class InvestorByBuyerSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByBuyerSpecification(string buyerId)
    {
        Query.Where(i => i.BuyerId == buyerId);
    }
}

public sealed class ModelByModelIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelByModelIdSpecification(string modelId)
    {
        Query.Where(m => m.ModelId == modelId).Include(m => m.Funds);
    }
}

public sealed class AllModelsSpecification : Specification<ModelPortfolio>
{
    public AllModelsSpecification()
    {
        Query.Include(m => m.Funds).OrderBy(m => m.ModelId);
    }
}

/// <summary>A shopper's payments, newest first. Scoped to the owning shopper.</summary>
public sealed class PaymentsByBuyerSpecification : Specification<PortfolioPayment>
{
    public PaymentsByBuyerSpecification(string buyerId)
    {
        Query.Where(p => p.BuyerId == buyerId).OrderByDescending(p => p.CreatedAt);
    }
}

public sealed class RealignmentByPublicIdSpecification : Specification<Realignment>, ISingleResultSpecification<Realignment>
{
    public RealignmentByPublicIdSpecification(Guid publicId)
    {
        Query.Where(r => r.PublicId == publicId);
    }
}
