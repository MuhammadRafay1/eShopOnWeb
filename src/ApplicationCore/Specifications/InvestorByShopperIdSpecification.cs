using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Selects the single <see cref="Investor"/> owned by a given shopper. This is the only way the
/// application looks an investor up, which is what keeps one shopper's data invisible to another.
/// </summary>
public sealed class InvestorByShopperIdSpecification : Specification<Investor>, ISingleResultSpecification<Investor>
{
    public InvestorByShopperIdSpecification(string shopperId, bool includeInvestments = false)
    {
        Query.Where(i => i.ShopperId == shopperId);

        if (includeInvestments)
        {
            Query.Include(i => i.Investments);
        }
    }
}
