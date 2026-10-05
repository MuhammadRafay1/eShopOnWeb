using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Enrols a shopper as an investor with the investment provider (Upvest).
/// </summary>
public interface IInvestorOnboardingService
{
    /// <summary>
    /// Enrol the given shopper. If they are already enrolled, their existing enrolment is
    /// returned unchanged (enrolment is idempotent per shopper). Otherwise a new enrolment is
    /// created and the shopper is registered with Upvest; the enrolment stays
    /// <see cref="EnrolmentStatus.Pending"/> until Upvest accepts them.
    /// </summary>
    Task<InvestingAccount> EnrolAsync(string buyerId, InvestorRegistration registration, CancellationToken cancellationToken = default);
}
