using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

public sealed class EnrolmentByShopperSpecification : Specification<Enrolment>, ISingleResultSpecification<Enrolment>
{
    public EnrolmentByShopperSpecification(string shopperId)
    {
        Query.Where(e => e.ShopperId == shopperId);
    }
}
