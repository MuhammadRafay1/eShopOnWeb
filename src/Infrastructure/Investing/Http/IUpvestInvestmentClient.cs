using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.Infrastructure.Investing.Http;

/// <summary>
/// Strongly-typed access to the Upvest Investment API endpoints this integration uses.
/// Every call goes through the single authenticating handler; no method attaches credentials.
/// </summary>
public interface IUpvestInvestmentClient
{
    // Onboarding.
    Task<UpvestResource> CreateUserAsync(InvestorRegistration registration, CancellationToken cancellationToken = default);
    Task SubmitKycCheckAsync(string userId, InvestorRegistration registration, CancellationToken cancellationToken = default);
    Task SubmitInstrumentFitCheckAsync(string userId, CancellationToken cancellationToken = default);
    Task SetTaxResidenciesAsync(string userId, string taxCountry, string taxId, CancellationToken cancellationToken = default);
    Task<UpvestResource> CreateAccountGroupAsync(string userId, CancellationToken cancellationToken = default);
    Task<UpvestResource> CreateAccountAsync(string userId, string accountGroupId, CancellationToken cancellationToken = default);

    // Status.
    Task<string> GetUserStatusAsync(string userId, CancellationToken cancellationToken = default);
    Task<string> GetAccountStatusAsync(string accountId, CancellationToken cancellationToken = default);

    // Investing.
    Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, CancellationToken cancellationToken = default);
    Task<UpvestResource> PlaceBuyOrderAsync(string userId, string accountId, decimal cashAmount, CancellationToken cancellationToken = default);
    Task<UpvestOrder> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    // Webhooks.
    Task<IReadOnlyList<UpvestWebhook>> ListWebhooksAsync(CancellationToken cancellationToken = default);
    Task<string> CreateWebhookAsync(string url, string title, CancellationToken cancellationToken = default);
    Task EnableWebhookAsync(string webhookId, string url, string title, CancellationToken cancellationToken = default);
    Task TestWebhookAsync(string webhookId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UpvestVerifyKey>> GetVerifyKeysAsync(CancellationToken cancellationToken = default);
}
