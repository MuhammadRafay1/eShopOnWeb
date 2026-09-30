using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentService : IPaymentService
{
    private static readonly PaymentStatus[] PaidStatuses = { PaymentStatus.Authorized, PaymentStatus.Captured, PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded };
    private static readonly PaymentStatus[] CapturedOrBeyond = { PaymentStatus.Captured, PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded };

    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<SavedCard> _savedCardRepository;
    private readonly IPayPalGateway _gateway;
    private readonly OrderPaymentLock _locks;
    private readonly string _instanceId;

    public PaymentService(
        IRepository<Payment> paymentRepository,
        IRepository<Order> orderRepository,
        IRepository<SavedCard> savedCardRepository,
        IPayPalGateway gateway,
        OrderPaymentLock locks,
        string instanceId)
    {
        _paymentRepository = paymentRepository;
        _orderRepository = orderRepository;
        _savedCardRepository = savedCardRepository;
        _gateway = gateway;
        _locks = locks;
        _instanceId = instanceId;
    }

    public async Task<PaymentActionResult> AuthorizePaymentAsync(int orderId, string buyerId, CardDetails? card, int? savedCardId, CancellationToken ct = default)
    {
        if ((card is null) == (savedCardId is null))
        {
            throw new ArgumentException("Provide exactly one of a card or a savedCardId to pay with.");
        }

        var payment = await LoadForBuyerAsync(orderId, buyerId, ct);
        if (PaidStatuses.Contains(payment.Status))
        {
            return Map(payment); // already authorized (or beyond) - idempotent no-op
        }
        if (payment.Status != PaymentStatus.AwaitingPayment)
        {
            throw new PaymentConflictException($"Order {orderId} is {payment.Status} and can no longer be paid.");
        }

        using (await _locks.AcquireAsync(orderId, ct))
        {
            payment = await LoadForBuyerAsync(orderId, buyerId, ct);
            if (PaidStatuses.Contains(payment.Status))
            {
                return Map(payment);
            }
            if (payment.Status != PaymentStatus.AwaitingPayment)
            {
                throw new PaymentConflictException($"Order {orderId} is {payment.Status} and can no longer be paid.");
            }

            string? vaultId = null;
            if (savedCardId.HasValue)
            {
                var savedCard = await _savedCardRepository.FirstOrDefaultAsync(new SavedCardByIdForBuyerSpec(savedCardId.Value, buyerId), ct)
                    ?? throw new NotFoundException($"No saved card {savedCardId} found for this shopper.");
                vaultId = savedCard.PayPalVaultId;
            }

            var requestId = $"auth-{_instanceId}-{orderId}";
            var result = await _gateway.AuthorizeAsync(payment.Amount, payment.CurrencyCode, card, vaultId, payment.InvoiceReference, requestId, ct);

            payment.MarkAuthorized(result.PayPalOrderId, result.AuthorizationId, result.AuthorizationStatus, result.ExpiresAt, result.CardBrand, result.CardLast4);
            await _paymentRepository.UpdateAsync(payment, ct);
            return Map(payment);
        }
    }

    public async Task<PaymentActionResult> FulfilOrderAsync(int orderId, CancellationToken ct = default)
    {
        var payment = await LoadAsync(orderId, ct);
        if (CapturedOrBeyond.Contains(payment.Status))
        {
            return Map(payment); // already fulfilled - idempotent no-op
        }
        if (payment.Status != PaymentStatus.Authorized)
        {
            throw new PaymentConflictException($"Order {orderId} is {payment.Status}; it must be Authorized before it can be fulfilled.");
        }

        using (await _locks.AcquireAsync(orderId, ct))
        {
            payment = await LoadAsync(orderId, ct);
            if (CapturedOrBeyond.Contains(payment.Status))
            {
                return Map(payment);
            }
            if (payment.Status != PaymentStatus.Authorized)
            {
                throw new PaymentConflictException($"Order {orderId} is {payment.Status}; it must be Authorized before it can be fulfilled.");
            }

            await EnsureAuthorizationIsFreshAsync(payment, ct);

            var requestId = $"capture-{_instanceId}-{orderId}";
            CaptureResult captureResult;
            try
            {
                captureResult = await _gateway.CaptureAsync(payment.AuthorizationId!, payment.Amount, payment.CurrencyCode, payment.InvoiceReference, requestId, ct);
            }
            catch (PayPalGatewayException captureFailure)
            {
                // The hold may have gone stale between our freshness check and this call.
                // Attempt exactly one renew-and-retry before surfacing an operator-actionable error.
                AuthorizeResult renewed;
                try
                {
                    renewed = await _gateway.ReauthorizeAsync(payment.AuthorizationId!, payment.Amount, payment.CurrencyCode, $"reauth-{_instanceId}-{orderId}-retry", ct);
                }
                catch (PayPalGatewayException)
                {
                    throw NotRenewable(orderId, captureFailure);
                }

                payment.RenewAuthorization(renewed.AuthorizationId, renewed.AuthorizationStatus, renewed.ExpiresAt);
                await _paymentRepository.UpdateAsync(payment, ct);

                captureResult = await _gateway.CaptureAsync(payment.AuthorizationId!, payment.Amount, payment.CurrencyCode, payment.InvoiceReference, requestId + "-retry", ct);
            }

            payment.MarkCaptured(captureResult.CaptureId, captureResult.GrossAmount, captureResult.PayPalFee, captureResult.NetAmount);
            await _paymentRepository.UpdateAsync(payment, ct);
            return Map(payment);
        }
    }

    public async Task<PaymentActionResult> CancelOrderAsync(int orderId, CancellationToken ct = default)
    {
        var payment = await LoadAsync(orderId, ct);
        if (payment.Status == PaymentStatus.Cancelled)
        {
            return Map(payment); // idempotent no-op
        }
        if (CapturedOrBeyond.Contains(payment.Status))
        {
            throw new PaymentConflictException($"Order {orderId} has already been captured; use a refund instead of cancel.");
        }
        if (payment.Status != PaymentStatus.Authorized)
        {
            throw new PaymentConflictException($"Order {orderId} is {payment.Status}; only an Authorized order can be cancelled.");
        }

        using (await _locks.AcquireAsync(orderId, ct))
        {
            payment = await LoadAsync(orderId, ct);
            if (payment.Status == PaymentStatus.Cancelled)
            {
                return Map(payment);
            }
            if (payment.Status != PaymentStatus.Authorized)
            {
                throw new PaymentConflictException($"Order {orderId} is {payment.Status} and cannot be cancelled.");
            }

            var requestId = $"void-{_instanceId}-{orderId}";
            await _gateway.VoidAsync(payment.AuthorizationId!, requestId, ct);

            payment.MarkCancelled();
            await _paymentRepository.UpdateAsync(payment, ct);
            return Map(payment);
        }
    }

    public async Task<RefundOutcome> RefundOrderAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An idempotencyKey is required for a refund.", nameof(idempotencyKey));
        }

        using (await _locks.AcquireAsync(orderId, ct))
        {
            var payment = await LoadForBuyerAsync(orderId, buyerId, ct);

            var existing = payment.Refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
            if (existing is not null)
            {
                return new RefundOutcome(existing.PayPalRefundId, existing.Status, existing.Amount, payment.TotalRefunded());
            }

            if (payment.Status is not (PaymentStatus.Captured or PaymentStatus.PartiallyRefunded))
            {
                throw new PaymentConflictException($"Order {orderId} is {payment.Status}; a refund is only possible after fulfilment.");
            }

            var captured = payment.CapturedAmount ?? 0m;
            var remaining = captured - payment.TotalRefunded();
            var refundAmount = amount ?? remaining;

            if (refundAmount <= 0)
            {
                throw new ArgumentException("Refund amount must be greater than zero.", nameof(amount));
            }
            if (refundAmount > remaining)
            {
                throw new PaymentConflictException($"Order {orderId} has only {remaining} left to refund (captured {captured}, already refunded {payment.TotalRefunded()}).");
            }

            // Scope the caller's idempotency key into a PayPal-Request-Id that is unique per
            // process instance/order: PayPal retains request-ids for 45 days, so a short,
            // caller-chosen key like "k1" could otherwise collide with an unrelated refund from
            // a different run against the same sandbox account.
            var payPalRequestId = $"refund-{_instanceId}-{orderId}-{idempotencyKey}";
            var refundResult = await _gateway.RefundAsync(payment.CaptureId!, refundAmount, payment.CurrencyCode, payPalRequestId, ct);

            payment.AddRefund(new Refund(refundResult.RefundId, refundAmount, refundResult.Status, idempotencyKey));
            await _paymentRepository.UpdateAsync(payment, ct);

            return new RefundOutcome(refundResult.RefundId, refundResult.Status, refundAmount, payment.TotalRefunded());
        }
    }

    public async Task<PaymentActionResult> GetPaymentForBuyerAsync(int orderId, string buyerId, CancellationToken ct = default)
    {
        var payment = await LoadForBuyerAsync(orderId, buyerId, ct);
        return Map(payment);
    }

    public async Task<IReadOnlyList<(int OrderId, DateTimeOffset OrderDate, PaymentActionResult Payment)>> GetOrdersForBuyerAsync(string buyerId, CancellationToken ct = default)
    {
        var payments = await _paymentRepository.ListAsync(new PaymentsByBuyerSpec(buyerId), ct);
        var result = new List<(int, DateTimeOffset, PaymentActionResult)>();
        foreach (var payment in payments)
        {
            var order = await _orderRepository.GetByIdAsync(payment.OrderId, ct);
            result.Add((payment.OrderId, order?.OrderDate ?? DateTimeOffset.MinValue, Map(payment)));
        }
        return result;
    }

    private async Task EnsureAuthorizationIsFreshAsync(Payment payment, CancellationToken ct)
    {
        var authInfo = await _gateway.GetAuthorizationAsync(payment.AuthorizationId!, ct);
        var expired = authInfo.ExpiresAt.HasValue && authInfo.ExpiresAt.Value <= DateTimeOffset.UtcNow.AddMinutes(1);
        var notCapturable = authInfo.Status is not ("CREATED" or "PENDING");

        if (!expired && !notCapturable)
        {
            return;
        }

        try
        {
            var renewed = await _gateway.ReauthorizeAsync(payment.AuthorizationId!, payment.Amount, payment.CurrencyCode, $"reauth-{_instanceId}-{payment.OrderId}", ct);
            payment.RenewAuthorization(renewed.AuthorizationId, renewed.AuthorizationStatus, renewed.ExpiresAt);
            await _paymentRepository.UpdateAsync(payment, ct);
        }
        catch (PayPalGatewayException ex)
        {
            throw NotRenewable(payment.OrderId, ex);
        }
    }

    private static AuthorizationNotRenewableException NotRenewable(int orderId, PayPalGatewayException cause) =>
        new($"The payment hold for order {orderId} expired and can no longer be renewed " +
            "(PayPal reauthorization is only possible on days 4-29 of the original authorization's honor period; " +
            "after day 30 a new payment must be taken). Ask the shopper to place and pay for a new order. " +
            cause.ToOperatorMessage());

    private async Task<Payment> LoadAsync(int orderId, CancellationToken ct)
    {
        return await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), ct)
            ?? throw new NotFoundException($"No order {orderId} found.");
    }

    private async Task<Payment> LoadForBuyerAsync(int orderId, string buyerId, CancellationToken ct)
    {
        var payment = await LoadAsync(orderId, ct);
        if (payment.BuyerId != buyerId)
        {
            throw new NotFoundException($"No order {orderId} found.");
        }
        return payment;
    }

    private static PaymentActionResult Map(Payment payment) => new(
        payment.OrderId,
        payment.Status.ToString(),
        payment.CurrencyCode,
        payment.Amount,
        payment.AuthorizationId,
        payment.CaptureId,
        payment.CapturedAmount,
        payment.PayPalFee,
        payment.NetAmount,
        payment.Refunds.Select(r => new RefundSummary(r.PayPalRefundId, r.Amount, r.Status)).ToList());
}
