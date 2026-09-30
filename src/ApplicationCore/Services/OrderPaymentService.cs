using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates authorize / fulfil / cancel / refund against PayPal, keeping the order state machine,
/// idempotency and ownership rules in one place. Depends only on the gateway abstraction and repositories,
/// so it is fully unit-testable without a network or the SDK.
/// </summary>
public class OrderPaymentService : IOrderPaymentService
{
    // Buffer before the PayPal-reported expiry at which we proactively renew a hold rather than risk a
    // capture racing the expiry moment.
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(60);

    private readonly IPayPalPaymentGateway _gateway;
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<OrderPayment> _paymentRepository;
    private readonly IRepository<SavedPaymentMethod> _savedCardRepository;
    private readonly IAppLogger<OrderPaymentService> _logger;

    public OrderPaymentService(
        IPayPalPaymentGateway gateway,
        IRepository<Order> orderRepository,
        IRepository<OrderPayment> paymentRepository,
        IRepository<SavedPaymentMethod> savedCardRepository,
        IAppLogger<OrderPaymentService> logger)
    {
        _gateway = gateway;
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _savedCardRepository = savedCardRepository;
        _logger = logger;
    }

    public async Task<AuthorizeActionResult> AuthorizeAsync(
        int orderId, string buyerId, CardDetails? card, int? savedPaymentMethodId, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var order = await LoadOwnedOrderWithItemsAsync(orderId, buyerId, ct);

        // (2) An existing payment row IS the idempotency check for a double-clicked pay.
        var existing = await _paymentRepository.FirstOrDefaultAsync(
            new OrderPaymentByOrderIdSpecification(orderId), ct);
        if (existing is not null)
        {
            return ToAuthorizeResult(order, existing);
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            throw new OrderStateConflictException($"Order {orderId} is cancelled and cannot be paid.");
        }
        if (order.Status != OrderStatus.AwaitingPayment)
        {
            throw new OrderStateConflictException(
                $"Order {orderId} cannot be paid because it is {order.Status}.");
        }

        // (3) Exactly one of card / saved card.
        var hasCard = card is not null;
        var hasSaved = savedPaymentMethodId.HasValue;
        if (hasCard == hasSaved)
        {
            throw new InvalidPaymentRequestException(
                "Provide exactly one of a card or a saved payment method id to pay with.");
        }

        var amount = order.Total();
        var currency = order.Currency;
        // Stable for a genuine double-click of the same order (same id + creation time => PayPal dedups the
        // race), but distinct across different order instances so a reused id never replays a stale PayPal
        // result. In production order ids are already unique; the timestamp just adds safety.
        var idempotencyKey = $"eshop-authorize-{orderId}-{order.OrderDate.Ticks}";

        AuthorizationResult authorization;
        int? usedSavedCardId = null;
        if (hasSaved)
        {
            var savedCard = await _savedCardRepository.GetByIdAsync(savedPaymentMethodId!.Value, ct);
            if (savedCard is null || savedCard.BuyerId != buyerId)
            {
                // Same 404-not-403 rule: never reveal another shopper's card exists.
                throw new PaymentMethodNotFoundException(savedPaymentMethodId!.Value);
            }
            usedSavedCardId = savedCard.Id;
            authorization = await _gateway.AuthorizeWithVaultedCardAsync(
                idempotencyKey, orderId, amount, currency, savedCard.PayPalVaultId, ct);
        }
        else
        {
            authorization = await _gateway.AuthorizeWithCardAsync(
                idempotencyKey, orderId, amount, currency, card!, ct);
        }

        // (5) 3DS / payer-action challenge — stop; do not build an approval round-trip.
        if (authorization.RequiresPayerAction)
        {
            throw new PayerActionRequiredException(orderId);
        }

        var payment = new OrderPayment(
            orderId,
            currency,
            authorization.PayPalOrderId,
            authorization.PayPalAuthorizationId,
            authorization.Amount,
            authorization.ExpiresAt,
            usedSavedCardId);

        // (6) Race guard: two concurrent first-time pays can both pass the check above. The unique index on
        // OrderPayment.OrderId makes the second insert fail; treat that exactly like the idempotent case.
        try
        {
            payment = await _paymentRepository.AddAsync(payment, ct);
        }
        catch (Exception ex)
        {
            var winner = await _paymentRepository.FirstOrDefaultAsync(
                new OrderPaymentByOrderIdSpecification(orderId), ct);
            if (winner is not null)
            {
                _logger.LogWarning("Concurrent authorize for order {0} lost the race; returning existing payment.", orderId);
                return ToAuthorizeResult(order, winner);
            }
            _logger.LogWarning("Persisting authorization for order {0} failed: {1}", orderId, ex.Message);
            throw;
        }

        order.MarkAuthorized();
        await _orderRepository.UpdateAsync(order, ct);

        return ToAuthorizeResult(order, payment);
    }

    public async Task<FulfilActionResult> FulfilAsync(int orderId, CancellationToken ct)
    {
        var order = await GetByIdOrThrowAsync(orderId, ct);
        var payment = await _paymentRepository.FirstOrDefaultAsync(
            new OrderPaymentByOrderIdSpecification(orderId), ct);

        // (1) Idempotent no-op if already fulfilled.
        if (order.Status is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded or OrderStatus.Refunded)
        {
            if (payment?.PayPalCaptureId is not null)
            {
                return ToFulfilResult(order, payment);
            }
        }
        if (order.Status != OrderStatus.Authorized || payment is null)
        {
            throw new OrderStateConflictException(
                $"Order {orderId} cannot be fulfilled because it is {order.Status}.");
        }

        // (2) Proactive staleness check before capture.
        if (DateTimeOffset.UtcNow >= payment.AuthorizationExpiresAt - ExpiryBuffer)
        {
            await RenewAuthorizationAsync(order, payment, ct);
        }

        CaptureResult capture;
        try
        {
            capture = await _gateway.CaptureAuthorizationAsync(
                CaptureKey(orderId, payment.PayPalAuthorizationId), payment.PayPalAuthorizationId, ct);
        }
        catch (Exception firstAttempt) when (firstAttempt is not OrderStateConflictException)
        {
            // (3) Fallback: the capture may have failed because the hold went stale between the proactive
            // check and the call. Attempt exactly one renew + recapture cycle.
            _logger.LogWarning("Capture for order {0} failed ({1}); attempting one reauthorize + recapture.",
                orderId, firstAttempt.Message);
            try
            {
                await RenewAuthorizationAsync(order, payment, ct);
                capture = await _gateway.CaptureAuthorizationAsync(
                    CaptureKey(orderId, payment.PayPalAuthorizationId), payment.PayPalAuthorizationId, ct);
            }
            catch (Exception)
            {
                // Unrecoverable: revert the order so the shopper can pay it again, and tell the operator why.
                // Remove the dead payment row so a re-pay creates a fresh authorization rather than short-
                // circuiting on this one (AuthorizeAsync treats an existing row as an idempotent success).
                await _paymentRepository.DeleteAsync(payment, ct);
                order.MarkAwaitingPayment();
                await _orderRepository.UpdateAsync(order, ct);
                throw new AuthorizationCannotBeRenewedException(orderId);
            }
        }

        payment.RecordCapture(capture.PayPalCaptureId, capture.CapturedAmount, capture.PayPalFeeAmount, capture.NetAmount);
        await _paymentRepository.UpdateAsync(payment, ct);

        order.MarkFulfilled();
        await _orderRepository.UpdateAsync(order, ct);

        return ToFulfilResult(order, payment);
    }

    public async Task<CancelActionResult> CancelAsync(int orderId, CancellationToken ct)
    {
        var order = await GetByIdOrThrowAsync(orderId, ct);

        if (order.Status == OrderStatus.Cancelled)
        {
            return new CancelActionResult(order.Id, order.Status); // idempotent no-op
        }
        if (order.Status is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded or OrderStatus.Refunded)
        {
            throw new OrderStateConflictException(
                $"Order {orderId} is {order.Status}; use a refund instead of cancel.");
        }

        if (order.Status == OrderStatus.AwaitingPayment)
        {
            // Never authorized — nothing to release on PayPal.
            order.MarkCancelled();
            await _orderRepository.UpdateAsync(order, ct);
            return new CancelActionResult(order.Id, order.Status);
        }

        // Authorized — release the hold on PayPal.
        var payment = await _paymentRepository.FirstOrDefaultAsync(
            new OrderPaymentByOrderIdSpecification(orderId), ct);
        if (payment is not null)
        {
            await _gateway.VoidAuthorizationAsync(
                VoidKey(orderId, payment.PayPalAuthorizationId), payment.PayPalAuthorizationId, ct);
            payment.RecordVoid();
            await _paymentRepository.UpdateAsync(payment, ct);
        }

        order.MarkCancelled();
        await _orderRepository.UpdateAsync(order, ct);
        return new CancelActionResult(order.Id, order.Status);
    }

    public async Task<RefundActionResult> RefundAsync(
        int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        var order = await LoadOwnedOrderAsync(orderId, buyerId, ct);
        var payment = await _paymentRepository.FirstOrDefaultAsync(
            new OrderPaymentByOrderIdSpecification(orderId), ct);

        if (payment?.PayPalCaptureId is null ||
            order.Status is not (OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded))
        {
            throw new OrderStateConflictException(
                $"Order {orderId} has no captured payment to refund (status {order.Status}).");
        }

        // (2) Idempotency by caller-supplied key — return the existing refund unchanged.
        var alreadyDone = payment.Refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
        if (alreadyDone is not null)
        {
            return ToRefundResult(order, payment, alreadyDone);
        }

        // (3) Over-refund guard, enforced before ever calling PayPal.
        var remaining = payment.RemainingRefundable();
        decimal refundAmount;
        if (amount.HasValue)
        {
            if (amount.Value <= 0m)
            {
                throw new InvalidPaymentRequestException("Refund amount must be greater than zero.");
            }
            if (amount.Value > remaining)
            {
                throw new InvalidPaymentRequestException(
                    $"Refund amount {amount.Value} exceeds the {remaining} still refundable on order {orderId}.");
            }
            refundAmount = amount.Value;
        }
        else
        {
            if (remaining <= 0m)
            {
                throw new InvalidPaymentRequestException(
                    $"Order {orderId} has nothing left to refund.");
            }
            refundAmount = remaining;
        }

        // Namespace by the globally-unique capture id so the caller's key dedups within this capture but can
        // never replay a PayPal refund made against a different capture (e.g. a reused order id across runs).
        var namespacedKey = $"eshop-refund-{payment.PayPalCaptureId}-{idempotencyKey}";
        var result = await _gateway.RefundCaptureAsync(
            namespacedKey, payment.PayPalCaptureId!, refundAmount, payment.Currency, ct);

        var refund = new Refund(result.PayPalRefundId, refundAmount, idempotencyKey);
        payment.RecordRefund(refund);
        try
        {
            await _paymentRepository.UpdateAsync(payment, ct);
        }
        catch (Exception ex)
        {
            // Unique (OrderPaymentId, IdempotencyKey) backstop: a racing duplicate with the same key.
            var reloaded = await _paymentRepository.FirstOrDefaultAsync(
                new OrderPaymentByOrderIdSpecification(orderId), ct);
            var winner = reloaded?.Refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
            if (reloaded is not null && winner is not null)
            {
                _logger.LogWarning("Concurrent refund for order {0} lost the race; returning existing refund.", orderId);
                return ToRefundResult(order, reloaded, winner);
            }
            _logger.LogWarning("Persisting refund for order {0} failed: {1}", orderId, ex.Message);
            throw;
        }

        if (payment.RemainingRefundable() <= 0m)
        {
            order.MarkRefunded();
        }
        else
        {
            order.MarkPartiallyRefunded();
        }
        await _orderRepository.UpdateAsync(order, ct);

        return ToRefundResult(order, payment, refund);
    }

    // --- helpers ---------------------------------------------------------------------------------

    private async Task RenewAuthorizationAsync(Order order, OrderPayment payment, CancellationToken ct)
    {
        var reauth = await _gateway.ReauthorizeAsync(
            ReauthorizeKey(order.Id, payment.PayPalAuthorizationId),
            payment.PayPalAuthorizationId, payment.AuthorizedAmount, payment.Currency, ct);
        payment.RecordReauthorization(reauth.PayPalAuthorizationId, reauth.ExpiresAt);
        await _paymentRepository.UpdateAsync(payment, ct);
    }

    private async Task<Order> LoadOwnedOrderWithItemsAsync(int orderId, string buyerId, CancellationToken ct)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), ct);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new OrderNotFoundException(orderId);
        }
        return order;
    }

    private async Task<Order> LoadOwnedOrderAsync(int orderId, string buyerId, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, ct);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new OrderNotFoundException(orderId);
        }
        return order;
    }

    private async Task<Order> GetByIdOrThrowAsync(int orderId, CancellationToken ct)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, ct);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }
        return order;
    }

    private static string CaptureKey(int orderId, string authId) => $"eshop-capture-{orderId}-{authId}";
    private static string VoidKey(int orderId, string authId) => $"eshop-void-{orderId}-{authId}";
    private static string ReauthorizeKey(int orderId, string authId) => $"eshop-reauthorize-{orderId}-{authId}";

    private static AuthorizeActionResult ToAuthorizeResult(Order order, OrderPayment payment) =>
        new(order.Id, order.Status, payment.AuthorizedAmount, payment.Currency, payment.AuthorizationExpiresAt);

    private static FulfilActionResult ToFulfilResult(Order order, OrderPayment payment) =>
        new(order.Id, order.Status, payment.CapturedAmount ?? 0m, payment.PayPalFeeAmount, payment.NetAmount, payment.Currency);

    private static RefundActionResult ToRefundResult(Order order, OrderPayment payment, Refund refund) =>
        new(refund.Id, refund.PayPalRefundId, order.Id, refund.Amount, order.Status, payment.RemainingRefundable());
}
