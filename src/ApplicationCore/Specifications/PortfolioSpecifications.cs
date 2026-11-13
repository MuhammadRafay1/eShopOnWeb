using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByShopperSpecification : Specification<InvestorRecord>, ISingleResultSpecification<InvestorRecord>
{
    public InvestorByShopperSpecification(string shopperUserName)
    {
        Query.Where(i => i.ShopperUserName == shopperUserName);
    }
}

public sealed class ModelPortfolioByModelIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByModelIdSpecification(string modelId)
    {
        Query.Where(m => m.ModelId == modelId);
    }
}

public sealed class PaymentsByShopperSpecification : Specification<PortfolioPayment>
{
    public PaymentsByShopperSpecification(string shopperUserName)
    {
        // Newest first.
        Query.Where(p => p.ShopperUserName == shopperUserName)
            .OrderByDescending(p => p.CreatedAt);
    }
}

public sealed class PendingPaymentsByShopperSpecification : Specification<PortfolioPayment>
{
    public PendingPaymentsByShopperSpecification(string shopperUserName)
    {
        Query.Where(p => p.ShopperUserName == shopperUserName
            && p.Status == PortfolioStatuses.Payment.Pending
            && p.UpvestOrderId != null);
    }
}

public sealed class RealignmentByIdSpecification : Specification<Realignment>, ISingleResultSpecification<Realignment>
{
    public RealignmentByIdSpecification(Guid realignmentId)
    {
        Query.Where(r => r.RealignmentId == realignmentId);
    }
}
