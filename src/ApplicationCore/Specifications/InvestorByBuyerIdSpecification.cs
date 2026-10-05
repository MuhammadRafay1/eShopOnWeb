using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads a single shopper's investor record, with their investments and
/// spare-change ledger. Scoped by buyer id so one shopper never sees another's.
/// </summary>
public sealed class InvestorByBuyerIdSpecification : Specification<Investor>
{
    public InvestorByBuyerIdSpecification(string buyerId)
    {
        Query.Where(i => i.BuyerId == buyerId)
            .Include(i => i.Investments)
            .Include(i => i.Ledger);
    }
}
