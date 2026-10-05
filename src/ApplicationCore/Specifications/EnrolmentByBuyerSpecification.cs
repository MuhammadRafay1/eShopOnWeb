using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class EnrolmentByBuyerSpecification : Specification<InvestorEnrolment>, ISingleResultSpecification<InvestorEnrolment>
{
    public EnrolmentByBuyerSpecification(string buyerId)
    {
        Query.Where(e => e.BuyerId == buyerId);
    }
}
