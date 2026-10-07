using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>A shopper's round-up for one eShop order, used to avoid setting the same order aside twice.</summary>
public sealed class RoundUpByOrderSpecification : Specification<RoundUp>
{
    public RoundUpByOrderSpecification(string buyerId, int orderId)
    {
        Query.Where(r => r.BuyerId == buyerId && r.OrderId == orderId);
    }
}
