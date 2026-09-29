using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>
/// Wraps the Payments v2 API: capture an authorization (fulfil), reauthorize a stale
/// authorization, void an authorization (cancel), and refund a capture.
/// </summary>
public interface IPayPalPaymentsClient
{
    /// <summary>POST /v2/payments/authorizations/{id}/capture (final_capture=true).</summary>
    Task<PayPalCaptureResult> CaptureAuthorizationAsync(
        int orderId, string authorizationId, decimal amount, string currencyCode,
        CancellationToken cancellationToken = default);

    /// <summary>POST /v2/payments/authorizations/{id}/reauthorize for a stale authorization.</summary>
    Task<PayPalAuthorizationResult> ReauthorizeAuthorizationAsync(
        int orderId, string authorizationId, decimal amount, string currencyCode,
        CancellationToken cancellationToken = default);

    /// <summary>POST /v2/payments/authorizations/{id}/void (releases the hold).</summary>
    Task VoidAuthorizationAsync(int orderId, string authorizationId, CancellationToken cancellationToken = default);

    /// <summary>GET /v2/payments/authorizations/{id}.</summary>
    Task<PayPalAuthorizationResult> GetAuthorizationAsync(string authorizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /v2/payments/captures/{id}/refund. A null amount means a full refund of the remaining
    /// captured amount. <paramref name="requestId"/> is the deterministic PayPal-Request-Id.
    /// </summary>
    Task<PayPalRefundResult> RefundCaptureAsync(
        string captureId, decimal? amount, string currencyCode, string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>GET /v2/payments/captures/{id}.</summary>
    Task<PayPalCaptureResult> GetCaptureAsync(string captureId, CancellationToken cancellationToken = default);
}
