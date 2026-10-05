using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's whole conversation with Upvest, expressed in domain terms. The implementation
/// wraps the Upvest SDK; all authentication is handled beneath it by a single delegating handler.
/// Every method throws <see cref="Exceptions.UpvestIntegrationException"/> on failure.
/// </summary>
public interface IUpvestInvestorGateway
{
    /// <summary>
    /// Onboard the shopper as a Upvest investor from the sign-up form (create the user, submit the KYC
    /// check and tax residency). Returns the Upvest user id.
    /// </summary>
    Task<Guid> EnrolInvestorAsync(EnrolmentForm form, CancellationToken cancellationToken);

    /// <summary>Where the shopper's acceptance (KYC) at Upvest currently stands.</summary>
    Task<InvestorStatus> GetAcceptanceStatusAsync(Guid upvestUserId, CancellationToken cancellationToken);

    /// <summary>
    /// The active Upvest account that holds the shopper's investments, provisioning the account group and
    /// account if needed. Null while the account is still being activated.
    /// </summary>
    Task<Guid?> TryResolveInvestmentAccountAsync(Guid upvestUserId, CancellationToken cancellationToken);

    /// <summary>Place a single buy order for the configured fund, for the given euro amount.</summary>
    Task<UpvestInvestmentPlacement> PlaceInvestmentAsync(Guid upvestAccountId, decimal amountInEuros, Guid idempotencyKey, CancellationToken cancellationToken);

    /// <summary>The current outcome of a placed order, mapped to this application's investment status.</summary>
    Task<InvestmentStatus> GetInvestmentOutcomeAsync(Guid upvestOrderId, CancellationToken cancellationToken);
}
