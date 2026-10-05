using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Opts a shopper in to investing their change by onboarding them as an
/// investor with Upvest.
/// </summary>
public interface IEnrolmentService
{
    /// <summary>
    /// Enrol the given shopper. Idempotent: if the shopper is already enrolled
    /// (and not rejected) their existing enrolment is returned unchanged.
    /// The returned enrolment starts <see cref="EnrolmentStatus.Pending"/> and
    /// becomes <see cref="EnrolmentStatus.Active"/> once Upvest accepts them.
    /// </summary>
    Task<Investor> EnrolAsync(string buyerId, EnrolmentDetails details, CancellationToken ct);

    /// <summary>The shopper's enrolment, or null if they have never opted in.</summary>
    Task<Investor?> GetEnrolmentAsync(string buyerId, CancellationToken ct);
}
