using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads a single shopper's investor together with their investments and spare-change ledger.
/// Scoped by <c>buyerId</c> so one shopper can never load another's.
/// </summary>
public class InvestorByBuyerIdSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByBuyerIdSpecification(string buyerId)
    {
        Query.Where(i => i.BuyerId == buyerId)
            .Include(i => i.Investments)
            .Include(i => i.Ledger);
    }
}
