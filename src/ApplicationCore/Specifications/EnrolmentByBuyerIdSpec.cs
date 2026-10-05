using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class EnrolmentByBuyerIdSpec : Specification<Enrolment>
{
    public EnrolmentByBuyerIdSpec(string buyerId) =>
        Query.Where(e => e.BuyerId == buyerId);
}
