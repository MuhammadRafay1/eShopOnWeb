using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// The payment processor, in the application's terms. Implementations translate every provider failure into
/// <see cref="Exceptions.PaymentProviderException"/> and bound every call by the request's time budget.
/// Write operations take the provider idempotency key stored on the operation's claim.
/// </summary>
public interface IPaymentGateway
{
    string ProviderName { get; }

    string Currency { get; }

    /// <summary>Creates a provider order for an authorization, carrying the payment source.</summary>
    Task<ProviderOrder> CreateAuthorizationOrderAsync(CreateAuthorizationOrderCommand command, CancellationToken cancellationToken);

    /// <summary>Places the hold for a provider order created with <see cref="CreateAuthorizationOrderAsync"/>.</summary>
    Task<ProviderOrder> AuthorizeOrderAsync(string providerOrderId, string providerRequestId, CancellationToken cancellationToken);

    Task<ProviderOrder> GetOrderAsync(string providerOrderId, CancellationToken cancellationToken);

    Task<ProviderAuthorization> GetAuthorizationAsync(string authorizationId, CancellationToken cancellationToken);

    Task<ProviderAuthorization> ReauthorizeAsync(string authorizationId, decimal amount, string currency,
        string providerRequestId, CancellationToken cancellationToken);

    Task<ProviderCapture> CaptureAsync(string authorizationId, decimal amount, string currency,
        string providerRequestId, CancellationToken cancellationToken);

    Task<ProviderCapture> GetCaptureAsync(string captureId, CancellationToken cancellationToken);

    Task<ProviderAuthorization> VoidAsync(string authorizationId, string providerRequestId, CancellationToken cancellationToken);

    Task<ProviderRefund> RefundAsync(string captureId, decimal amount, string currency, string customId,
        string providerRequestId, CancellationToken cancellationToken);

    Task<ProviderVaultedCard> SaveCardAsync(CardDetails card, string? providerCustomerId, string providerRequestId,
        CancellationToken cancellationToken);

    Task DeleteSavedCardAsync(string vaultTokenId, CancellationToken cancellationToken);

    /// <summary>One page of the provider's transaction report; the range must not exceed <see cref="MaxSearchWindow"/>.</summary>
    Task<ProviderTransactionPage> SearchTransactionsAsync(DateTimeOffset from, DateTimeOffset to, int page, int pageSize,
        CancellationToken cancellationToken);

    TimeSpan MaxSearchWindow { get; }
}
