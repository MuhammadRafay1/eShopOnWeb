using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>The single investor for a shop identity (at most one).</summary>
public sealed class InvestorByBuyerIdSpec : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByBuyerIdSpec(string buyerId) => Query.Where(i => i.BuyerId == buyerId);
}

/// <summary>Investors whose enrolment is still pending and have an account to reconcile against.</summary>
public sealed class PendingEnrolmentsSpec : Specification<Investor>
{
    public PendingEnrolmentsSpec() =>
        Query.Where(i => i.Status == EnrolmentStatus.Pending && i.UpvestAccountId != null);
}

/// <summary>A shopper's investments, newest first.</summary>
public sealed class InvestmentsByInvestorSpec : Specification<Investment>
{
    public InvestmentsByInvestorSpec(int investorId) =>
        Query.Where(x => x.InvestorId == investorId).OrderByDescending(x => x.CreatedAt);
}

/// <summary>Investments still awaiting their final outcome at the provider.</summary>
public sealed class PendingInvestmentsSpec : Specification<Investment>
{
    public PendingInvestmentsSpec() => Query.Where(x => x.Status == InvestmentStatus.Pending);
}
