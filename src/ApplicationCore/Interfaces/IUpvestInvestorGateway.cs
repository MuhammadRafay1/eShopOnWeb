using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's boundary to Upvest. Everything the shop needs from Upvest is expressed here in
/// the shop's own terms; the implementation (in Infrastructure) is the only code that knows the Upvest
/// SDK. Keeping this interface free of SDK types stops Upvest's shape from leaking into the domain.
/// </summary>
public interface IUpvestInvestorGateway
{
    /// <summary>
    /// Submit a shopper's sign-up form to Upvest: create the user, submit the KYC check and declare tax
    /// residency. Returns the user and check references. Does not wait for acceptance — Upvest activates
    /// the user asynchronously once the check passes.
    /// </summary>
    Task<UpvestEnrolmentResult> EnrolInvestorAsync(InvestorSignUpForm form, CancellationToken cancellationToken);

    /// <summary>
    /// Provision (or reuse) the PERSONAL account group and TRADING account for an accepted investor.
    /// Must only be called once Upvest has activated the user, as the account group cannot be created
    /// before then.
    /// </summary>
    Task<UpvestAccountRefs> ProvisionAccountsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The shopper's current enrolment status at Upvest: <see cref="EnrolmentStatus.Active"/> once the
    /// user is active, <see cref="EnrolmentStatus.Rejected"/> if their KYC check failed, otherwise
    /// <see cref="EnrolmentStatus.Pending"/>.
    /// </summary>
    Task<EnrolmentStatus> GetEnrolmentStatusAsync(Guid userId, Guid kycCheckId, CancellationToken cancellationToken);

    /// <summary>
    /// Invest <paramref name="amountEur"/> of the shopper's set-aside change into the configured fund:
    /// fund the account group with the cash and place a single nominal market buy order. Returns the
    /// Upvest order id.
    /// </summary>
    Task<Guid> InvestAsync(Guid accountGroupId, Guid accountId, Guid userId, decimal amountEur, CancellationToken cancellationToken);

    /// <summary>The current status at Upvest of a previously placed investment order.</summary>
    Task<InvestmentStatus> GetInvestmentStatusAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Best-effort: ensure a webhook subscription exists so Upvest notifies this application of status
    /// changes. Safe to call repeatedly. Never throws.
    /// </summary>
    Task EnsureWebhookSubscriptionAsync(CancellationToken cancellationToken);
}
