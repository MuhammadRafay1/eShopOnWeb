using System;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A single model portfolio with its funds loaded.</summary>
public sealed class ModelPortfolioByIdSpec : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByIdSpec(string modelId)
    {
        Query.Where(m => m.ModelId == modelId)
             .Include(m => m.Funds);
    }
}

/// <summary>Every model portfolio with its funds loaded.</summary>
public sealed class ModelPortfoliosSpec : Specification<ModelPortfolio>
{
    public ModelPortfoliosSpec()
    {
        Query.Include(m => m.Funds)
             .OrderBy(m => m.ModelId);
    }
}

/// <summary>A shopper's payments, newest first.</summary>
public sealed class PaymentsByShopperSpec : Specification<Payment>
{
    public PaymentsByShopperSpec(string shopperId)
    {
        Query.Where(p => p.ShopperId == shopperId)
             .OrderByDescending(p => p.CreatedDate);
    }
}

/// <summary>A shopper's pending payments (used to reconcile outcomes against Upvest).</summary>
public sealed class PendingPaymentsByShopperSpec : Specification<Payment>
{
    public PendingPaymentsByShopperSpec(string shopperId)
    {
        Query.Where(p => p.ShopperId == shopperId && p.Status == PaymentStatus.Pending);
    }
}
