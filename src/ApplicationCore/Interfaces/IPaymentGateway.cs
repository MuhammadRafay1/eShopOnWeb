using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Abstraction over the payment processor for order money movement. ApplicationCore defines what it needs
/// without knowing PayPal exists; Infrastructure implements this against the PayPal SDK. All methods are
/// idempotent in effect: the implementation derives a deterministic provider request-id from the supplied
/// reference so a retried call never moves money twice.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Authorize (hold) the order total. Does not take the money.</summary>
    Task<AuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken);

    /// <summary>Capture (take) a previously authorized payment at fulfilment.</summary>
    Task<CaptureResult> CaptureAsync(string orderReference, string authorizationId, Money amount, CancellationToken cancellationToken);

    /// <summary>Renew a stale authorization so a capture can proceed.</summary>
    Task<ReauthorizationResult> ReauthorizeAsync(string orderReference, string authorizationId, Money amount, CancellationToken cancellationToken);

    /// <summary>Void an authorization (cancel before fulfilment), releasing the held funds.</summary>
    Task VoidAsync(string orderReference, string authorizationId, CancellationToken cancellationToken);

    /// <summary>Refund a captured payment in full (null amount) or in part. The idempotency key is forwarded to PayPal.</summary>
    Task<RefundResult> RefundAsync(string captureId, Money? amount, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Re-read a PayPal order to settle an unknown write outcome.</summary>
    Task<OrderSnapshot> GetOrderSnapshotAsync(string payPalOrderId, CancellationToken cancellationToken);
}
