using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Loads a single shopper's investing account with its investments and set-aside entries.</summary>
public sealed class InvestingAccountByBuyerSpec : Specification<InvestingAccount>, ISingleResultSpecification<InvestingAccount>
{
    public InvestingAccountByBuyerSpec(string buyerId)
    {
        Query
            .Where(a => a.BuyerId == buyerId)
            .Include(a => a.Investments)
            .Include(a => a.SpareChangeEntries);
    }
}
