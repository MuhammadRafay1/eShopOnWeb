using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates the money movement for an order: authorize (hold) at pay time, capture at
/// fulfilment (renewing a stale hold), void at cancel, and refund after fulfilment. Idempotency is
/// enforced at two independent layers — an app-level state check here, plus the deterministic
/// PayPal-Request-Id the client sends — so a double-click can never charge the shopper twice.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentMethod> _paymentMethodRepository;
    private readonly IPayPalClient _payPalClient;
    private readonly string _currency;

    public PaymentService(
        IRepository<Order> orderRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentMethod> paymentMethodRepository,
        IPayPalClient payPalClient,
        IOptions<PaymentSettings> paymentSettings)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _payPalClient = payPalClient;
        _currency = paymentSettings.Value.Currency;
    }

    public async Task<OrderPaymentResult> PayAsync(int orderId, string buyerId, CardDetails? card, int? paymentMethodId)
    {
        // Exactly one funding source: raw card XOR saved card.
        if ((card is null) == (paymentMethodId is null))
            throw new PaymentValidationException(
                "Provide exactly one of 'card' (one-off) or 'paymentMethodId' (saved card).");

        var order = await _orderRepository.FirstOrDefaultAsync(new OrderByIdAndBuyerIdSpec(orderId, buyerId));
        if (order is null)
            throw new ResourceNotFoundException($"Order {orderId} was not found.");

        // App-level idempotency: a payment already exists for this order → nothing new happens.
        var existingPayment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId));
        if (existingPayment is not null)
            throw new PaymentConflictException(
                $"Order {orderId} has already been paid (payment status: {existingPayment.Status}).");

        if (order.Status != OrderStatus.AwaitingPayment)
            throw new PaymentConflictException(
                $"Order {orderId} cannot be paid in its current state ({order.Status}).");

        var amount = order.Total();
        if (amount <= 0)
            throw new PaymentValidationException("Order total must be greater than zero to authorize a payment.");

        AuthorizationResult authorization;
        if (paymentMethodId is not null)
        {
            var savedCard = await _paymentMethodRepository.FirstOrDefaultAsync(
                new PaymentMethodByIdAndBuyerIdSpec(paymentMethodId.Value, buyerId));
            if (savedCard is null)
                throw new ResourceNotFoundException($"Saved card {paymentMethodId} was not found.");

            authorization = await _payPalClient.AuthorizeWithVaultedCardAsync(orderId, _currency, amount, savedCard.PayPalVaultId);
        }
        else
        {
            authorization = await _payPalClient.AuthorizeWithCardAsync(orderId, _currency, amount, card!);
        }

        var payment = new Payment(orderId, _currency, amount, authorization.PayPalOrderId,
            authorization.AuthorizationId, authorization.Status, authorization.ExpiresAt);
        await _paymentRepository.AddAsync(payment);

        order.MarkPaymentAuthorized();
        await _orderRepository.UpdateAsync(order);

        return new OrderPaymentResult(order, payment);
    }

    public async Task<OrderPaymentResult> FulfilAsync(int orderId)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId));
        if (order is null)
            throw new ResourceNotFoundException($"Order {orderId} was not found.");

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId));

        // Idempotency short-circuit: already captured (or later) → nothing new happens.
        if (order.Status is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded or OrderStatus.Refunded)
            throw new PaymentConflictException($"Order {orderId} has already been fulfilled.");

        if (order.Status != OrderStatus.PaymentAuthorized || payment is null)
            throw new PaymentConflictException(
                $"Order {orderId} cannot be fulfilled in its current state ({order.Status}).");

        // Always read the hold's live state, then branch on documented fields (not guessed codes).
        var details = await _payPalClient.GetAuthorizationAsync(payment.PayPalAuthorizationId);

        if (!string.Equals(details.Status, "CREATED", StringComparison.OrdinalIgnoreCase))
        {
            // e.g. DENIED / VOIDED — cannot capture and cannot renew.
            var reason = details.StatusReason is null ? details.Status : $"{details.Status} ({details.StatusReason})";
            throw new PaymentConflictException(
                $"The authorization for order {orderId} is in state '{reason}' and cannot be captured.");
        }

        var authorizationId = payment.PayPalAuthorizationId;
        var expired = details.ExpiresAt is not null && DateTimeOffset.UtcNow >= details.ExpiresAt.Value;
        if (expired)
        {
            // Renew a stale hold before capturing. The idempotency key derives from the pre-increment
            // count, so a client retry of the same fulfil call reuses the key rather than double-renewing.
            var reauthKey = $"paypal-reauth-{orderId}-{payment.ReauthorizationCount}";
            try
            {
                var renewed = await _payPalClient.ReauthorizeAsync(authorizationId, payment.Currency, payment.Amount, reauthKey);
                payment.RenewAuthorization(renewed.AuthorizationId, renewed.Status, renewed.ExpiresAt);
                await _paymentRepository.UpdateAsync(payment);
                authorizationId = renewed.AuthorizationId;
            }
            catch (PayPalApiException ex)
            {
                payment.RecordRenewalFailure(ex.Message);
                await _paymentRepository.UpdateAsync(payment);
                throw new AuthorizationUnrenewableException(ex.Message);
            }
        }

        var capture = await _payPalClient.CaptureAsync(authorizationId, $"paypal-capture-{orderId}");
        payment.Capture(capture.CaptureId, capture.Status, capture.Amount, capture.PayPalFee, capture.NetAmount);
        await _paymentRepository.UpdateAsync(payment);

        order.MarkFulfilled();
        await _orderRepository.UpdateAsync(order);

        return new OrderPaymentResult(order, payment);
    }

    public async Task<OrderPaymentResult> CancelAsync(int orderId)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId));
        if (order is null)
            throw new ResourceNotFoundException($"Order {orderId} was not found.");

        if (order.Status is not (OrderStatus.AwaitingPayment or OrderStatus.PaymentAuthorized))
            throw new PaymentConflictException(
                $"Order {orderId} cannot be cancelled in its current state ({order.Status}).");

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId));

        if (order.Status == OrderStatus.PaymentAuthorized && payment is not null && payment.Status == PaymentStatus.Authorized)
        {
            // Release the held funds — no money ever moved.
            await _payPalClient.VoidAsync(payment.PayPalAuthorizationId, $"paypal-void-{orderId}");
            payment.Void();
            await _paymentRepository.UpdateAsync(payment);
        }

        order.MarkCancelled();
        await _orderRepository.UpdateAsync(order);

        return new OrderPaymentResult(order, payment);
    }

    public async Task<RefundOperationResult> RefundAsync(int orderId, decimal? amount, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new PaymentValidationException("An idempotencyKey is required for a refund.");

        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order is null)
            throw new ResourceNotFoundException($"Order {orderId} was not found.");

        if (order.Status is not (OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded))
            throw new PaymentConflictException(
                $"Order {orderId} cannot be refunded in its current state ({order.Status}).");

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId));
        if (payment is null || payment.PayPalCaptureId is null)
            throw new PaymentConflictException($"Order {orderId} has no captured payment to refund.");

        // Idempotency: a refund already recorded under this key returns as-is — no second PayPal call.
        var replay = payment.Refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
        if (replay is not null)
            return new RefundOperationResult(replay, payment, order, WasReplay: true);

        if (amount is not null)
        {
            if (amount.Value <= 0)
                throw new PaymentValidationException("Refund amount must be greater than zero.");
            if (amount.Value > payment.RefundableRemaining)
                throw new PaymentValidationException(
                    $"Refund amount {amount.Value} exceeds the refundable remaining {payment.RefundableRemaining}.");
        }

        var payPalRefund = await _payPalClient.RefundAsync(payment.PayPalCaptureId, payment.Currency, amount, $"paypal-refund-{orderId}-{idempotencyKey}");

        var refundAmount = amount ?? payPalRefund.Amount;
        var refund = new Refund(payPalRefund.RefundId, idempotencyKey, refundAmount, payPalRefund.Status);
        payment.RecordRefund(refund);

        if (payment.Status == PaymentStatus.Refunded)
            order.MarkFullyRefunded();
        else
            order.MarkPartiallyRefunded();

        await _paymentRepository.UpdateAsync(payment);
        await _orderRepository.UpdateAsync(order);

        return new RefundOperationResult(refund, payment, order, WasReplay: false);
    }

    public async Task<IReadOnlyList<OrderPaymentResult>> GetMyOrdersAsync(string buyerId)
    {
        var orders = await _orderRepository.ListAsync(new OrdersByBuyerIdSpec(buyerId));
        if (orders.Count == 0)
            return new List<OrderPaymentResult>();

        var payments = await _paymentRepository.ListAsync(
            new PaymentsByOrderIdsSpec(orders.Select(o => o.Id)));
        var paymentsByOrderId = payments.ToDictionary(p => p.OrderId);

        return orders
            .Select(o => new OrderPaymentResult(o, paymentsByOrderId.GetValueOrDefault(o.Id)))
            .ToList();
    }
}
