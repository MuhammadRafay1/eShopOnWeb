using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Abstraction over every call this application makes to Upvest. Implementations route all
/// traffic through the single authenticating <c>DelegatingHandler</c>; no call site attaches
/// credentials itself.
/// </summary>
public interface IUpvestClient
{
    // --- Onboarding ---
    Task<UpvestUser> CreateUserAsync(InvestorSignUp form, CancellationToken cancellationToken = default);
    Task CreateTaxResidencyAsync(string userId, string taxCountry, string taxId, CancellationToken cancellationToken = default);
    Task CreateKycCheckAsync(string userId, InvestorSignUp form, CancellationToken cancellationToken = default);
    Task CreateProofOfResidencyCheckAsync(string userId, InvestorSignUp form, CancellationToken cancellationToken = default);
    Task<UpvestUser> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Statuses of the user's regulatory checks (e.g. "PASSED", "FAILED", "CREATED").</summary>
    Task<IReadOnlyList<string>> GetCheckStatusesAsync(string userId, CancellationToken cancellationToken = default);

    Task<UpvestAccountGroup> CreateAccountGroupAsync(string userId, CancellationToken cancellationToken = default);
    Task<UpvestAccount> CreateAccountAsync(string userId, string accountGroupId, CancellationToken cancellationToken = default);
    Task<UpvestAccount> GetAccountAsync(string accountId, CancellationToken cancellationToken = default);

    // --- Investing ---
    Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, CancellationToken cancellationToken = default);
    Task<UpvestOrder> PlaceBuyOrderAsync(string userId, string accountId, decimal cashAmount, CancellationToken cancellationToken = default);
    Task<UpvestOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    // --- Webhooks ---
    Task EnsureWebhookAsync(string callbackUrl, CancellationToken cancellationToken = default);

    /// <summary>The current set of public keys used to verify inbound webhook signatures.</summary>
    Task<IReadOnlyList<UpvestJwk>> GetVerificationKeysAsync(CancellationToken cancellationToken = default);
}
