using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>The investor record belonging to one shopper (by token identity).</summary>
public class InvestorByBuyerIdSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByBuyerIdSpecification(string buyerId)
    {
        Query.Where(i => i.BuyerId == buyerId);
    }
}

/// <summary>A model portfolio by its operator-chosen id, with its funds.</summary>
public class ModelPortfolioByModelIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByModelIdSpecification(string modelId)
    {
        Query.Where(m => m.ModelId == modelId)
            .Include(m => m.Funds);
    }
}

/// <summary>Every model portfolio with its funds.</summary>
public class ModelPortfoliosWithFundsSpecification : Specification<ModelPortfolio>
{
    public ModelPortfoliosWithFundsSpecification()
    {
        Query.Include(m => m.Funds)
            .OrderBy(m => m.ModelId);
    }
}

/// <summary>A caller's payments, newest first.</summary>
public class PaymentsByBuyerIdSpecification : Specification<Payment>
{
    public PaymentsByBuyerIdSpecification(string buyerId)
    {
        Query.Where(p => p.BuyerId == buyerId)
            .OrderByDescending(p => p.CreatedAt);
    }
}

/// <summary>A realignment by its app id.</summary>
public class RealignmentByIdSpecification : Specification<Realignment>, ISingleResultSpecification<Realignment>
{
    public RealignmentByIdSpecification(System.Guid realignmentId)
    {
        Query.Where(r => r.RealignmentId == realignmentId);
    }
}
