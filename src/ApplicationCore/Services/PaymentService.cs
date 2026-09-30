using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentService : IPaymentService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentMethod> _paymentMethodRepository;
    private readonly IPayPalGateway _payPal;
    private readonly PayPalOptions _options;
    private readonly IAppLogger<PaymentService> _logger;

    public PaymentService(
        IRepository<Order> orderRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentMethod> paymentMethodRepository,
        IPayPalGateway payPal,
        PayPalOptions options,
        IAppLogger<PaymentService> logger)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _payPal = payPal;
        _options = options;
        _logger = logger;
    }

    private string Currency => _options.Currency;

    public async Task<Payment> AuthorizeOrderAsync(int orderId, string buyerId, PaymentCard? card,
        int? paymentMethodId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        if ((card is null) == (paymentMethodId is null))
        {
            throw new ArgumentException("Exactly one of a card or a saved paymentMethodId must be supplied.");
        }

        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), cancellationToken);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new OrderNotFoundException(orderId);
        }

        // Server-side idempotency guard: a second pay on an already-authorized order returns the
        // existing payment rather than authorizing again (protects against double-clicks).
        var existing = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), cancellationToken);
        if (existing is not null && order.Status == OrderStatus.PaymentAuthorized)
        {
            return existing;
        }
        if (order.Status != OrderStatus.AwaitingPayment)
        {
            throw new InvalidOrderStateException(
                $"Order {orderId} cannot be paid because it is {order.Status}; only an order awaiting payment can be paid.");
        }

        var amount = order.Total();
        if (amount <= 0)
        {
            throw new InvalidOrderStateException($"Order {orderId} has a non-positive total and cannot be paid.");
        }
        var invoiceId = BuildInvoiceId(orderId);

        PayPalAuthorizationResult auth;
        string requestId;
        if (paymentMethodId is not null)
        {
            var method = await _paymentMethodRepository.GetByIdAsync(paymentMethodId.Value, cancellationToken);
            if (method is null || method.BuyerId != buyerId)
            {
                throw new PaymentMethodNotFoundException(paymentMethodId.Value);
            }
            requestId = RequestId("authorize", orderId, method.PayPalVaultId, amount);
            auth = await _payPal.AuthorizeWithVaultAsync(amount, Currency, invoiceId, method.PayPalVaultId, requestId, cancellationToken);
        }
        else
        {
            // Keyed by a hash of the card's last-4 + expiry (not the raw PAN) so a legitimate retry
            // with a *different* card is a fresh authorization, while an identical resubmission dedupes.
            requestId = RequestId("authorize", orderId, card!.Number[^4..] + card.Expiry, amount);
            auth = await _payPal.AuthorizeWithCardAsync(amount, Currency, invoiceId, card, requestId, cancellationToken);
        }

        var payment = new Payment(orderId, Currency, amount, auth.PayPalOrderId, invoiceId,
            auth.AuthorizationId, auth.Status, auth.ExpiresAt, requestId, paymentMethodId);

        await _paymentRepository.AddAsync(payment, cancellationToken);

        order.SetStatus(OrderStatus.PaymentAuthorized);
        await _orderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation("Order {0} authorized (payment {1}, authorization {2}).", orderId, payment.Id, auth.AuthorizationId);
        return payment;
    }

    public async Task<Payment> FulfilOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), cancellationToken);
        if (payment is null)
        {
            throw new InvalidOrderStateException($"Order {orderId} has no authorized payment to fulfil.");
        }

        // Idempotent short-circuit for a repeated fulfil.
        if (order.Status is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded or OrderStatus.Refunded
            && payment.IsCaptured)
        {
            return payment;
        }
        if (order.Status != OrderStatus.PaymentAuthorized)
        {
            throw new InvalidOrderStateException(
                $"Order {orderId} cannot be fulfilled because it is {order.Status}; only an authorized order can be fulfilled.");
        }

        var authorizationId = payment.PayPalAuthorizationId!;
        var captureRequestId = RequestId("capture", payment.Id, authorizationId, payment.Amount);
        PayPalCaptureResult capture;
        try
        {
            capture = await _payPal.CaptureAsync(authorizationId, payment.Amount, payment.Currency,
                payment.InvoiceId, captureRequestId, cancellationToken);
        }
        catch (PayPalAuthorizationExpiredException)
        {
            // The hold went stale before fulfilment — renew it once, then retry the capture against
            // the new authorization. If it can no longer be renewed, surface an operator-actionable error.
            _logger.LogWarning("Authorization {0} for order {1} expired; attempting to reauthorize.", authorizationId, orderId);
            PayPalAuthorizationResult reauth;
            try
            {
                reauth = await _payPal.ReauthorizeAsync(authorizationId, payment.Amount, payment.Currency,
                    RequestId("reauth", payment.Id, authorizationId, payment.Amount), cancellationToken);
            }
            catch (PayPalApiException ex)
            {
                throw new AuthorizationNotRenewableException(ex.Issue, ex.Description);
            }

            payment.RecordReauthorization(reauth.AuthorizationId, reauth.Status, reauth.ExpiresAt);
            await _paymentRepository.UpdateAsync(payment, cancellationToken);

            captureRequestId = RequestId("capture", payment.Id, reauth.AuthorizationId, payment.Amount);
            capture = await _payPal.CaptureAsync(reauth.AuthorizationId, payment.Amount, payment.Currency,
                payment.InvoiceId, captureRequestId, cancellationToken);
        }

        payment.RecordCapture(capture.CaptureId, capture.Status, capture.GrossAmount, capture.PayPalFee,
            capture.NetAmount, captureRequestId);
        order.SetStatus(OrderStatus.Fulfilled);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        await _orderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation("Order {0} fulfilled (capture {1}, net {2} {3}).", orderId, capture.CaptureId, capture.NetAmount, payment.Currency);
        return payment;
    }

    public async Task<Payment> CancelOrderAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), cancellationToken);

        if (order.Status == OrderStatus.Cancelled)
        {
            return payment!; // idempotent
        }
        if (order.Status != OrderStatus.PaymentAuthorized || payment is null)
        {
            throw new InvalidOrderStateException(
                $"Order {orderId} cannot be cancelled because it is {order.Status}; only an authorized, unfulfilled order can be cancelled.");
        }

        await _payPal.VoidAsync(payment.PayPalAuthorizationId!,
            RequestId("void", payment.Id, payment.PayPalAuthorizationId!, payment.Amount), cancellationToken);

        payment.RecordVoid();
        order.SetStatus(OrderStatus.Cancelled);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        await _orderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation("Order {0} cancelled; authorization {1} voided.", orderId, payment.PayPalAuthorizationId);
        return payment;
    }

    public async Task<RefundReceipt> RefundOrderAsync(int orderId, string buyerId, string idempotencyKey,
        decimal? amount, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new OrderNotFoundException(orderId);
        }

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), cancellationToken);
        if (payment is null || !payment.IsCaptured)
        {
            throw new InvalidOrderStateException($"Order {orderId} has not been fulfilled and cannot be refunded.");
        }
        if (order.Status is not (OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded))
        {
            throw new InvalidOrderStateException(
                $"Order {orderId} cannot be refunded because it is {order.Status}.");
        }

        // Idempotency fast path: a repeat under the same key returns the stored refund, no PayPal call.
        var priorRefund = payment.Refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
        if (priorRefund is not null)
        {
            return new RefundReceipt(priorRefund.Id, priorRefund.Status, priorRefund.Amount,
                payment.Currency, payment.RemainingRefundable, order.Status.ToString());
        }

        var refundAmount = amount ?? payment.RemainingRefundable;
        if (refundAmount <= 0)
        {
            throw new ArgumentException("Refund amount must be positive.", nameof(amount));
        }
        // Local cap guards the invariant even before relying on PayPal's own captured-minus-refunds cap.
        if (refundAmount > payment.RemainingRefundable)
        {
            throw new RefundAmountExceedsRemainingException(refundAmount, payment.RemainingRefundable);
        }

        var result = await _payPal.RefundAsync(payment.PayPalCaptureId!, refundAmount, payment.Currency,
            payment.InvoiceId, RequestId("refund", payment.Id, idempotencyKey, refundAmount), cancellationToken);

        var refund = payment.RecordRefund(result.RefundId, refundAmount, result.Status, idempotencyKey);
        order.SetStatus(payment.RemainingRefundable <= 0 ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        await _orderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation("Order {0} refunded {1} {2} (refund {3}).", orderId, refundAmount, payment.Currency, result.RefundId);
        return new RefundReceipt(refund.Id, refund.Status, refund.Amount, payment.Currency,
            payment.RemainingRefundable, order.Status.ToString());
    }

    public async Task<PaymentMethod> SaveCardAsync(string buyerId, PaymentCard card,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(card, nameof(card));

        var vaulted = await _payPal.VaultCardAsync(card, $"savecard:{Guid.NewGuid():N}", cancellationToken);
        var method = new PaymentMethod(buyerId, vaulted.VaultId, vaulted.CustomerId,
            vaulted.Brand, vaulted.Last4, vaulted.Expiry);
        await _paymentMethodRepository.AddAsync(method, cancellationToken);

        _logger.LogInformation("Shopper {0} saved a {1} card ending {2} (payment method {3}).", buyerId, vaulted.Brand, vaulted.Last4, method.Id);
        return method;
    }

    public async Task<IReadOnlyList<PaymentMethod>> GetSavedCardsAsync(string buyerId,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        return await _paymentMethodRepository.ListAsync(new PaymentMethodsByBuyerSpec(buyerId), cancellationToken);
    }

    public async Task DeleteSavedCardAsync(int paymentMethodId, string buyerId,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var method = await _paymentMethodRepository.GetByIdAsync(paymentMethodId, cancellationToken);
        if (method is null || method.BuyerId != buyerId)
        {
            throw new PaymentMethodNotFoundException(paymentMethodId);
        }

        try
        {
            await _payPal.DeleteVaultedCardAsync(method.PayPalVaultId, cancellationToken);
        }
        catch (PayPalApiException ex) when (ex.StatusCode == 404)
        {
            // Already gone on PayPal's side — treat as success.
            _logger.LogWarning("Vault token {0} was already absent on PayPal (404); deleting local record.", method.PayPalVaultId);
        }
        catch (PayPalApiException ex)
        {
            // The card must no longer be usable through this API regardless of PayPal's vault state,
            // which is fully within our control. Remove locally and flag the stale vault entry for
            // out-of-band cleanup rather than leaving the shopper with an undeletable card.
            _logger.LogWarning("Failed to delete vault token {0} on PayPal ({1}); deleting local record anyway. Vault entry may need manual cleanup.", method.PayPalVaultId, ex.Message);
        }

        await _paymentMethodRepository.DeleteAsync(method, cancellationToken);
        _logger.LogInformation("Shopper {0} deleted saved payment method {1}.", buyerId, paymentMethodId);
    }

    public async Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            throw new ArgumentException("'from' must not be after 'to'.");
        }

        var transactions = await _payPal.SearchTransactionsAsync(from, to, cancellationToken);
        var payments = await _paymentRepository.ListAsync(new PaymentsInDateRangeSpec(from, to), cancellationToken);

        var matched = new List<ReconciliationMatch>();
        var payPalOnly = new List<PayPalOnlyTransaction>();
        var matchedPayments = new HashSet<Payment>(); // reference equality — robust to duplicate ids

        foreach (var txn in transactions)
        {
            var payment = payments.FirstOrDefault(p =>
                (!string.IsNullOrEmpty(txn.InvoiceId) && string.Equals(txn.InvoiceId, p.InvoiceId, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(txn.ReferenceId) &&
                    (txn.ReferenceId == p.PayPalAuthorizationId || txn.ReferenceId == p.PayPalCaptureId || txn.ReferenceId == p.PayPalOrderId)));

            if (payment is not null)
            {
                matchedPayments.Add(payment);
                matched.Add(new ReconciliationMatch(payment.OrderId, payment.Id, txn.TransactionId,
                    payment.CaptureStatus ?? payment.AuthorizationStatus, txn.Status, payment.Amount, txn.Amount));
            }
            else
            {
                payPalOnly.Add(new PayPalOnlyTransaction(txn.TransactionId, txn.ReferenceId, txn.InvoiceId,
                    txn.Amount, txn.Currency, txn.Status));
            }
        }

        var eShopOnly = payments
            .Where(p => !matchedPayments.Contains(p))
            .Select(p => new EShopOnlyPayment(p.OrderId, p.Id,
                p.CaptureStatus ?? p.AuthorizationStatus, p.Amount, p.Currency))
            .ToList();

        return new ReconciliationReport(from, to, matched, payPalOnly, eShopOnly);
    }

    // --- Idempotency key derivation (PayPal-Request-Id) ---

    private static string RequestId(string operation, int id, string discriminator, decimal amount)
    {
        var payload = $"{operation}:{id}:{discriminator}:{amount.ToString("0.00", CultureInfo.InvariantCulture)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return $"{operation}-{id}-{Convert.ToHexString(hash)[..32]}";
    }

    // Unique per authorization so it is a reliable reconciliation join key even when the in-memory
    // store restarts order ids from 1 across runs (PayPal treats invoice_id as unique per merchant).
    private static string BuildInvoiceId(int orderId) => $"ESHOP-{orderId}-{Guid.NewGuid():N}"[..24];
}
