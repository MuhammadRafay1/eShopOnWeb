using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>The investor record owned by a single shopper.</summary>
public sealed class InvestorByShopperSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByShopperSpecification(string shopperId)
    {
        Query.Where(i => i.ShopperId == shopperId);
    }
}

/// <summary>All model portfolios with their funds.</summary>
public sealed class ModelPortfoliosWithFundsSpecification : Specification<ModelPortfolio>
{
    public ModelPortfoliosWithFundsSpecification()
    {
        Query.Include(m => m.Funds).OrderBy(m => m.Id);
    }
}

/// <summary>A single model portfolio with its funds.</summary>
public sealed class ModelPortfolioByIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByIdSpecification(int modelId)
    {
        Query.Where(m => m.Id == modelId).Include(m => m.Funds);
    }
}

/// <summary>A shopper's payments, newest first.</summary>
public sealed class PaymentsByShopperSpecification : Specification<Payment>
{
    public PaymentsByShopperSpecification(string shopperId)
    {
        Query.Where(p => p.ShopperId == shopperId).OrderByDescending(p => p.CreatedAt);
    }
}

/// <summary>A realignment by its externally exposed id.</summary>
public sealed class RealignmentByPublicIdSpecification : Specification<Realignment>, ISingleResultSpecification<Realignment>
{
    public RealignmentByPublicIdSpecification(Guid publicId)
    {
        Query.Where(r => r.PublicId == publicId);
    }
}
