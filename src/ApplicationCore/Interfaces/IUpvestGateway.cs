using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The application's single door to Upvest. Implemented in Infrastructure over the Upvest SDK; every call
/// it makes is authenticated by the one reusable signing handler, so no caller attaches credentials.
/// </summary>
public interface IUpvestGateway
{
    /// <summary>
    /// Register the shopper as an Upvest user and submit their KYC and tax residency. Returns the Upvest user
    /// id. Upvest then accepts the user asynchronously; call <see cref="TryCompleteEnrolmentAsync"/> to find out.
    /// </summary>
    Task<string> RegisterInvestorAsync(InvestorEnrolmentDetails details, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-read where the shopper's enrolment has got to. Once Upvest has accepted the user, this also ensures
    /// the holding account group and trading account exist and returns their ids.
    /// </summary>
    Task<UpvestEnrolment> TryCompleteEnrolmentAsync(string upvestUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invest the given amount of euros for the shopper in the configured fund by placing a single BUY order,
    /// funding the account beforehand so the order can execute.
    /// </summary>
    Task<UpvestOrderResult> PlaceInvestmentOrderAsync(string upvestUserId, string upvestAccountGroupId, string upvestAccountId, decimal amountEuros, CancellationToken cancellationToken = default);

    /// <summary>Read the current status of a previously placed investment order.</summary>
    Task<InvestmentStatus> GetOrderStatusAsync(string upvestOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensure Upvest will call this application back about order events, registering the callback webhook if
    /// it is not already present. Best-effort: settlement is also reconciled on read, so a missed webhook is
    /// not fatal.
    /// </summary>
    Task EnsureOrderWebhookAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Where an enrolment stands at Upvest. The account ids are populated only once the shopper has been
/// accepted (<see cref="EnrolmentStatus.Active"/>).
/// </summary>
public sealed record UpvestEnrolment(EnrolmentStatus Status, string? AccountGroupId, string? AccountId);

/// <summary>Outcome of placing an investment order with Upvest.</summary>
public sealed record UpvestOrderResult(string OrderId, InvestmentStatus Status);
