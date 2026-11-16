using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class InvestorByShopperIdSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByShopperIdSpecification(string shopperId)
    {
        Query.Where(i => i.ShopperId == shopperId);
    }
}

public sealed class ModelPortfolioByModelIdSpecification : Specification<ModelPortfolio>, ISingleResultSpecification<ModelPortfolio>
{
    public ModelPortfolioByModelIdSpecification(string modelId)
    {
        Query.Where(m => m.ModelId == modelId).Include(m => m.Funds);
    }
}

public sealed class ModelPortfoliosWithFundsSpecification : Specification<ModelPortfolio>
{
    public ModelPortfoliosWithFundsSpecification()
    {
        Query.Include(m => m.Funds).OrderBy(m => m.ModelId);
    }
}

public sealed class PaymentsByShopperIdSpecification : Specification<Payment>
{
    public PaymentsByShopperIdSpecification(string shopperId)
    {
        // Newest first.
        Query.Where(p => p.ShopperId == shopperId).OrderByDescending(p => p.CreatedAt);
    }
}

public sealed class PaymentByIdSpecification : Specification<Payment>, ISingleResultSpecification<Payment>
{
    public PaymentByIdSpecification(System.Guid paymentId, string shopperId)
    {
        // Scoped to the owning shopper so one shopper can never read another's payment.
        Query.Where(p => p.PaymentId == paymentId && p.ShopperId == shopperId);
    }
}

public sealed class RealignmentByIdSpecification : Specification<Realignment>, ISingleResultSpecification<Realignment>
{
    public RealignmentByIdSpecification(System.Guid realignmentId)
    {
        Query.Where(r => r.RealignmentId == realignmentId);
    }
}

public sealed class RealignmentsByModelIdSpecification : Specification<Realignment>
{
    public RealignmentsByModelIdSpecification(string modelId)
    {
        Query.Where(r => r.ModelId == modelId);
    }
}
