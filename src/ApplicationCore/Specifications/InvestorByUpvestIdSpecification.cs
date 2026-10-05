using System;
using System.Linq;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Selects the investor referenced by an Upvest id — their user id, account group id, or one of their
/// orders. Used to reconcile the record an Upvest callback refers to without trusting the payload's contents.
/// </summary>
public sealed class InvestorByUpvestIdSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByUpvestIdSpecification(Guid upvestId)
    {
        Query
            .Where(i => i.UpvestUserId == upvestId
                        || i.UpvestAccountId == upvestId
                        || i.Investments.Any(inv => inv.UpvestOrderId == upvestId))
            .Include(i => i.Investments);
    }
}
