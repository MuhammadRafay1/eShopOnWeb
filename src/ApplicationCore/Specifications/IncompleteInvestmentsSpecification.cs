using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>Investments whose outcome at Upvest is not yet settled or failed.</summary>
public sealed class IncompleteInvestmentsSpecification : Specification<Investment>
{
    public IncompleteInvestmentsSpecification()
    {
        Query.Where(i => i.Stage != InvestmentStage.Settled
                      && i.Stage != InvestmentStage.Failed)
             .OrderBy(i => i.CreatedAt);
    }
}
