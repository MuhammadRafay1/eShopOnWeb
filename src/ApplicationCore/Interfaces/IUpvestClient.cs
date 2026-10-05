using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Investing;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// A thin gateway over the Upvest Investment API. Each method maps to a single
/// Upvest endpoint; orchestration lives in the services that use this gateway.
/// Every call is authenticated (OAuth token + HTTP message signature) by a
/// single delegating handler — no method here attaches credentials itself.
/// </summary>
public interface IUpvestClient
{
    // ---- Onboarding ----
    Task<UpvestUserRef> CreateUserAsync(EnrolmentDetails details, CancellationToken ct);
    Task SubmitKycCheckAsync(string upvestUserId, EnrolmentDetails details, CancellationToken ct);
    Task SubmitInstrumentFitCheckAsync(string upvestUserId, CancellationToken ct);
    Task SetTaxResidenciesAsync(string upvestUserId, string taxCountry, string? taxId, CancellationToken ct);
    Task<UpvestUserRef> GetUserAsync(string upvestUserId, CancellationToken ct);
    Task<UpvestAccountGroupRef> CreateAccountGroupAsync(string upvestUserId, CancellationToken ct);
    Task<UpvestAccountRef> CreateAccountAsync(string upvestUserId, string accountGroupId, CancellationToken ct);
    Task<UpvestAccountRef> GetAccountAsync(string accountId, CancellationToken ct);

    // ---- Investing ----
    Task IncreaseVirtualCashAsync(string accountGroupId, decimal amount, CancellationToken ct);
    Task<UpvestOrderRef> PlaceBuyOrderAsync(string upvestUserId, string accountId, decimal cashAmount, CancellationToken ct);
    Task<UpvestOrderRef> GetOrderAsync(string orderId, CancellationToken ct);

    // ---- Webhooks (best-effort; settlement also reconciles by polling) ----
    Task<string?> CreateEnabledWebhookAsync(string callbackUrl, CancellationToken ct);
}
