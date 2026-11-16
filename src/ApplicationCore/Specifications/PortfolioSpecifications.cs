using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByUserNameSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByUserNameSpecification(string shopperUserName) =>
        Query.Where(i => i.ShopperUserName == shopperUserName);
}

public sealed class ModelPortfolioByKeySpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByKeySpecification(string modelKey) =>
        Query.Where(m => m.ModelKey == modelKey).Include(m => m.Funds);
}

public sealed class ModelPortfolioByIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByIdSpecification(int id) =>
        Query.Where(m => m.Id == id).Include(m => m.Funds);
}

public sealed class AllModelPortfoliosSpecification : Specification<ModelPortfolio>
{
    public AllModelPortfoliosSpecification() =>
        Query.Include(m => m.Funds).OrderBy(m => m.ModelKey);
}

public sealed class PaymentsByUserSpecification : Specification<PortfolioPayment>
{
    // Newest first.
    public PaymentsByUserSpecification(string shopperUserName) =>
        Query.Where(p => p.ShopperUserName == shopperUserName).OrderByDescending(p => p.CreatedAt);
}

public sealed class RealignmentByKeySpecification : Specification<PortfolioRealignment>, ISingleResultSpecification<PortfolioRealignment>
{
    public RealignmentByKeySpecification(Guid realignmentKey) =>
        Query.Where(r => r.RealignmentKey == realignmentKey);
}
