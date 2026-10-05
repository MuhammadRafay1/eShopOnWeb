using System.Linq;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads accounts that have at least one investment still in flight, so their outcome can be
/// reconciled against Upvest.
/// </summary>
public sealed class AccountsWithPendingInvestmentsSpec : Specification<InvestingAccount>
{
    public AccountsWithPendingInvestmentsSpec()
    {
        Query
            .Where(a => a.Investments.Any(i => i.Status == InvestmentStatus.Pending))
            .Include(a => a.Investments)
            .Include(a => a.SpareChangeEntries);
    }
}
