using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's whole conversation with Upvest, expressed in domain terms. The implementation owns the
/// SDK, the authentication (OAuth bearer + request signing) and the error boundary; nothing above this
/// interface knows about the Upvest SDK.
/// </summary>
public interface IUpvestInvestingGateway
{
    /// <summary>
    /// Begin onboarding the shopper as an investor: create the Upvest user and submit the identity (KYC) and
    /// tax-residency records that let Upvest accept them. Returns the Upvest user id. Acceptance (and the
    /// account to hold investments) follows asynchronously — drive it with <see cref="AdvanceOnboardingAsync"/>.
    /// <paramref name="idempotencyScope"/> is a stable per-shopper string so a retry reuses the same keys.
    /// </summary>
    Task<Guid> CreateInvestorAsync(InvestorSignUp signUp, string idempotencyScope, CancellationToken cancellationToken);

    /// <summary>
    /// Advance onboarding using what is already known: once Upvest has accepted the user, create the account
    /// group and trading account if not yet created, then report whether the shopper can invest (account
    /// ACTIVE). Safe to call repeatedly; it only does the work still outstanding.
    /// </summary>
    Task<OnboardingProgress> AdvanceOnboardingAsync(Guid userId, Guid? accountGroupId, Guid? accountId, string idempotencyScope, CancellationToken cancellationToken);

    /// <summary>
    /// Place a BUY of the configured fund for the given amount (euros) on the shopper's account.
    /// <paramref name="clientReference"/> is carried on the order so an ambiguous placement can be found again;
    /// <paramref name="idempotencyScope"/> is a stable string keyed to the investment.
    /// </summary>
    Task<UpvestInvestmentResult> PlaceInvestmentAsync(Guid userId, Guid accountGroupId, Guid accountId, decimal amountEuros, string clientReference, string idempotencyScope, CancellationToken cancellationToken);

    /// <summary>Re-read an order's outcome at Upvest to settle a pending investment.</summary>
    Task<InvestmentStatus> GetInvestmentOutcomeAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Reconcile a placement whose outcome was unknown: find the order on the account by its client reference.
    /// Returns null when no such order is found (the write did not land).
    /// </summary>
    Task<UpvestInvestmentResult?> FindInvestmentByReferenceAsync(Guid accountId, string clientReference, CancellationToken cancellationToken);
}
