using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Owns the order-payment lifecycle: authorize (hold) -> fulfil (capture) -> refund, or
/// cancel (void) before fulfilment. Also owns saved-card (vault) management and the
/// PayPal-vs-eShop reconciliation report. Idempotency, ownership checks and PayPal-state
/// bookkeeping all live here so endpoints stay thin.
/// </summary>
public interface IPaymentService
{
    Task<Payment> AuthorizeAsync(int orderId, string buyerId, PaymentAuthorizeInput input, CancellationToken cancellationToken);

    Task<Payment> FulfilAsync(int orderId, CancellationToken cancellationToken);

    Task<Payment> CancelAsync(int orderId, CancellationToken cancellationToken);

    Task<PaymentRefundResult> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken);

    Task<PaymentMethod> SavePaymentMethodAsync(string buyerId, PayPalCardInput card, CancellationToken cancellationToken);

    Task DeletePaymentMethodAsync(int paymentMethodId, string buyerId, CancellationToken cancellationToken);

    Task<ReconciliationReport> GetReconciliationReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
