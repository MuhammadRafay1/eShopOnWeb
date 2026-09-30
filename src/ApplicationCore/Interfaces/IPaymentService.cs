using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Orchestrates the money-movement lifecycle: authorize (hold), fulfil (capture), cancel (void),
/// refund, and saved-card management. Owns the order/payment state machine, idempotency guards and
/// persistence, delegating the actual PayPal calls to <see cref="IPayPalGateway"/>. Ownership is
/// enforced here (a shopper acts only on their own data); a mismatch is surfaced as "not found".
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Authorize an order's total with PayPal — a hold, not a capture. Exactly one of
    /// <paramref name="card"/> / <paramref name="paymentMethodId"/> must be supplied.
    /// </summary>
    Task<Payment> AuthorizeOrderAsync(int orderId, string buyerId, PaymentCard? card,
        int? paymentMethodId, CancellationToken cancellationToken = default);

    /// <summary>Fulfil an authorized order — capture the held funds (renewing a stale hold if needed).</summary>
    Task<Payment> FulfilOrderAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>Cancel an authorized-but-unfulfilled order — void the hold so no money moves.</summary>
    Task<Payment> CancelOrderAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>Refund a fulfilled order's capture, in full or in part, idempotently by key.</summary>
    Task<RefundReceipt> RefundOrderAsync(int orderId, string buyerId, string idempotencyKey,
        decimal? amount, CancellationToken cancellationToken = default);

    /// <summary>Vault a card for the shopper and record a safe local description of it.</summary>
    Task<PaymentMethod> SaveCardAsync(string buyerId, PaymentCard card,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentMethod>> GetSavedCardsAsync(string buyerId,
        CancellationToken cancellationToken = default);

    /// <summary>Remove a saved card: delete it from PayPal's vault and from the local store.</summary>
    Task DeleteSavedCardAsync(int paymentMethodId, string buyerId,
        CancellationToken cancellationToken = default);

    Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
