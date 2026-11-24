using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByBuyerIdSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByBuyerIdSpecification(string buyerId)
    {
        Query.Where(i => i.BuyerId == buyerId);
    }
}

public sealed class ModelPortfolioByModelIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByModelIdSpecification(string modelId)
    {
        Query.Include(m => m.Funds).Where(m => m.ModelId == modelId);
    }
}

public sealed class ModelPortfoliosWithFundsSpecification : Specification<ModelPortfolio>
{
    public ModelPortfoliosWithFundsSpecification()
    {
        Query.Include(m => m.Funds).OrderBy(m => m.ModelId);
    }
}

public sealed class PaymentsByInvestorSpecification : Specification<PortfolioPayment>
{
    public PaymentsByInvestorSpecification(int investorId)
    {
        Query.Where(p => p.InvestorId == investorId).OrderByDescending(p => p.CreatedAt);
    }
}

public sealed class PaymentByPublicIdSpecification : Specification<PortfolioPayment>, ISingleResultSpecification<PortfolioPayment>
{
    public PaymentByPublicIdSpecification(Guid publicId)
    {
        Query.Where(p => p.PublicId == publicId);
    }
}

public sealed class RealignmentByPublicIdSpecification : Specification<PortfolioRealignment>, ISingleResultSpecification<PortfolioRealignment>
{
    public RealignmentByPublicIdSpecification(Guid publicId)
    {
        Query.Where(r => r.PublicId == publicId);
    }
}
