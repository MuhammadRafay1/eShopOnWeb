using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The shop's sole doorway to Upvest. Every method maps to one or more Upvest API operations; the
/// implementation lives in Infrastructure and owns all Upvest SDK, signing and credential concerns.
/// </summary>
public interface IUpvestInvestingGateway
{
    /// <summary>
    /// Creates the investor's Upvest user and submits the regulatory checks and tax residency.
    /// The user starts out pending and is accepted asynchronously. Throws
    /// <see cref="UpvestRejectedException"/> when Upvest refuses the sign-up outright.
    /// </summary>
    Task<UpvestUserRef> EnrolUserAsync(InvestorSignup signup, CancellationToken cancellationToken = default);

    /// <summary>Reads the investor's current acceptance status from Upvest.</summary>
    Task<EnrolmentStatus> GetUserStatusAsync(string upvestUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions the account group and trading account the investments are held in. Valid only once
    /// the user has been accepted (Upvest rejects this earlier).
    /// </summary>
    Task<UpvestAccountRef> ProvisionAccountAsync(string upvestUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invests <paramref name="amountEuros"/> for the shopper in the configured instrument, returning
    /// the Upvest order id and its initial status.
    /// </summary>
    Task<UpvestInvestmentRef> PlaceInvestmentAsync(string upvestUserId, string upvestAccountId, decimal amountEuros, CancellationToken cancellationToken = default);

    /// <summary>Reads the current status of a previously placed investment order from Upvest.</summary>
    Task<InvestmentStatus> GetOrderStatusAsync(string upvestOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a webhook subscription exists and is active, delivering status-change events to the
    /// given callback URL. Best effort — a failure here does not stop the application.
    /// </summary>
    Task EnsureWebhookSubscriptionAsync(string callbackUrl, CancellationToken cancellationToken = default);
}
