using System;
using Ardalis.Specification;

namespace Microsoft.eShopWeb.ApplicationCore.Investing.Specifications;

public sealed class EnrolmentByShopperSpecification : Specification<InvestorEnrolment>, ISingleResultSpecification<InvestorEnrolment>
{
    public EnrolmentByShopperSpecification(string shopperId) =>
        Query.Where(e => e.ShopperId == shopperId);
}

public sealed class PendingEnrolmentsSpecification : Specification<InvestorEnrolment>
{
    public PendingEnrolmentsSpecification() =>
        Query.Where(e => e.Status == EnrolmentStatus.Pending);
}

public sealed class LedgerByShopperSpecification : Specification<SpareChangeLedger>, ISingleResultSpecification<SpareChangeLedger>
{
    public LedgerByShopperSpecification(string shopperId) =>
        Query.Where(l => l.ShopperId == shopperId);
}

public sealed class InvestmentsByShopperSpecification : Specification<Investment>
{
    public InvestmentsByShopperSpecification(string shopperId) =>
        Query.Where(i => i.ShopperId == shopperId).OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id);
}

/// <summary>Investments that still need work: created-but-not-placed, or placed-and-awaiting-outcome.</summary>
public sealed class UnsettledInvestmentsSpecification : Specification<Investment>
{
    public UnsettledInvestmentsSpecification() =>
        Query.Where(i => i.Status == InvestmentStatus.Created || i.Status == InvestmentStatus.Placed);
}

public sealed class InvestmentByUpvestOrderIdSpecification : Specification<Investment>, ISingleResultSpecification<Investment>
{
    public InvestmentByUpvestOrderIdSpecification(string upvestOrderId) =>
        Query.Where(i => i.UpvestOrderId == upvestOrderId);
}
