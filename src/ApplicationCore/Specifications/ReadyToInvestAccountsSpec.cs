using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Specifications;

/// <summary>
/// Loads accepted investors whose set-aside balance has reached the investment threshold.
/// </summary>
public sealed class ReadyToInvestAccountsSpec : Specification<InvestingAccount>
{
    public ReadyToInvestAccountsSpec()
    {
        Query
            .Where(a => a.Status == EnrolmentStatus.Active
                        && a.UpvestAccountId != null
                        && a.PendingAmount >= InvestingAccount.InvestmentThreshold)
            .Include(a => a.Investments)
            .Include(a => a.SpareChangeEntries);
    }
}
