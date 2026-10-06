using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>
/// The application's port onto the investment provider (Upvest). The infrastructure implementation owns
/// every detail of how the provider is called (authentication, signing, the provider SDK); callers here
/// speak only in domain terms. All calls may throw <see cref="UpvestGatewayException"/> on provider failure.
/// </summary>
public interface IUpvestGateway
{
    /// <summary>
    /// Phase one of registration: creates the Upvest user, submits the required check, records tax residency,
    /// and subscribes a webhook. The user activates asynchronously; the account is provisioned separately.
    /// </summary>
    Task<UpvestInvestorRegistration> RegisterInvestorAsync(
        InvestorEnrolmentData data, CancellationToken cancellationToken);

    /// <summary>
    /// Phase two: provisions the account group and trading account for a now-active user. Throws
    /// <see cref="UpvestGatewayException"/> with <see cref="HttpStatusCode.Conflict"/> if the user is not yet
    /// active, which the caller treats as "still pending".
    /// </summary>
    Task<UpvestAccountProvision> ProvisionAccountAsync(Guid upvestUserId, CancellationToken cancellationToken);

    /// <summary>Reads the current enrolment status implied by the account's status at the provider.</summary>
    Task<EnrolmentStatus> GetAccountStatusAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Invests one tranche: funds the account group with the amount, then places a nominal market buy order
    /// for the configured instrument.
    /// </summary>
    Task<UpvestInvestmentPlacement> PlaceInvestmentAsync(
        UpvestInvestmentInstruction instruction, CancellationToken cancellationToken);

    /// <summary>Reads the current status of a placed investment order from the provider.</summary>
    Task<InvestmentStatus> GetInvestmentStatusAsync(Guid upvestOrderId, CancellationToken cancellationToken);

    /// <summary>
    /// Reconciles an investment whose placement outcome is unknown (the order id was never captured) by
    /// looking the order up on the account by its <c>client_reference</c>. Returns the found order's id and
    /// status, or null if no matching order exists at the provider.
    /// </summary>
    Task<UpvestInvestmentPlacement?> FindInvestmentByReferenceAsync(
        Guid accountId, string clientReference, CancellationToken cancellationToken);
}
