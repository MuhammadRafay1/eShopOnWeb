using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

/// <summary>
/// The authorize/capture/reauthorize/void/refund lifecycle against PayPal's Orders v2 and
/// Payments v2 APIs. Implementations throw <see cref="Exceptions.PayPalApprovalRequiredException"/>
/// when PayPal asks for a shopper-facing approval (a hard stop), and
/// <see cref="Exceptions.PayPalIntegrationException"/> for transport/unexpected failures.
/// </summary>
public interface IPayPalPaymentGateway
{
    /// <summary>Authorizes (holds) the order total. Does not capture.</summary>
    Task<PayPalAuthorizationOutcome> AuthorizeAsync(PayPalAuthorizeRequest request, CancellationToken ct);

    /// <summary>Captures (takes) a previously authorized amount in full.</summary>
    Task<PayPalCaptureOutcome> CaptureAsync(string authorizationId, decimal amount, string currency,
        string orderIdForInvoice, string idempotencyKey, CancellationToken ct);

    /// <summary>Renews a stale authorization (same authorization id, fresh honor period).</summary>
    Task<PayPalReauthorizeOutcome> ReauthorizeAsync(string authorizationId, decimal amount,
        string currency, CancellationToken ct);

    /// <summary>Voids (releases) a live authorization.</summary>
    Task VoidAsync(string authorizationId, CancellationToken ct);

    /// <summary>Refunds a capture, in full (amount null) or in part.</summary>
    Task<PayPalRefundOutcome> RefundAsync(string captureId, decimal? amount, string currency,
        string idempotencyKey, CancellationToken ct);
}
