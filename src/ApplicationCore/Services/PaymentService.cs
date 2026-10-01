using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentService : IPaymentService
{
    private readonly IReadRepository<Order> _orderReadRepository;
    private readonly IRepository<OrderPayment> _paymentRepository;
    private readonly IReadRepository<VaultedPaymentMethod> _paymentMethodReadRepository;
    private readonly IPayPalPaymentGateway _gateway;
    private readonly IPaymentOperationClaimStore _claimStore;
    private readonly PayPalSettings _settings;
    private readonly IAppLogger<PaymentService> _logger;

    public PaymentService(
        IReadRepository<Order> orderReadRepository,
        IRepository<OrderPayment> paymentRepository,
        IReadRepository<VaultedPaymentMethod> paymentMethodReadRepository,
        IPayPalPaymentGateway gateway,
        IPaymentOperationClaimStore claimStore,
        PayPalSettings settings,
        IAppLogger<PaymentService> logger)
    {
        _orderReadRepository = orderReadRepository;
        _paymentRepository = paymentRepository;
        _paymentMethodReadRepository = paymentMethodReadRepository;
        _gateway = gateway;
        _claimStore = claimStore;
        _settings = settings;
        _logger = logger;
    }

    public async Task<OrderPayment> PayAsync(int orderId, string buyerId, PayPalCardInput? card, int? paymentMethodId, CancellationToken cancellationToken)
    {
        var order = await LoadOwnedOrderAsync(orderId, buyerId, cancellationToken);
        var payment = await _paymentRepository.FirstOrDefaultAsync(new OrderPaymentByOrderIdSpec(orderId), cancellationToken);

        if (payment is not null && payment.Status != OrderPaymentStatus.AwaitingPayment)
        {
            // Idempotent: a double-click (or a retried request) after the order is already authorized
            // (or further along) just returns the current state rather than re-authorizing.
            return payment;
        }

        payment ??= await _paymentRepository.AddAsync(new OrderPayment(orderId, buyerId, _settings.Currency), cancellationToken);

        string? vaultId = null;
        if (paymentMethodId is not null)
        {
            var paymentMethod = await _paymentMethodReadRepository.FirstOrDefaultAsync(new VaultedPaymentMethodByIdSpec(paymentMethodId.Value), cancellationToken);
            if (paymentMethod is null || paymentMethod.BuyerId != buyerId)
            {
                throw new PaymentMethodNotFoundException(paymentMethodId.Value);
            }
            vaultId = paymentMethod.PayPalVaultId;
        }

        var claimed = await _claimStore.TryClaimAsync(ClaimKey(orderId, "authorize"), cancellationToken);
        if (!claimed)
        {
            return await _paymentRepository.FirstOrDefaultAsync(new OrderPaymentByOrderIdSpec(orderId), cancellationToken) ?? payment;
        }

        var amount = order.Total();
        var result = await _gateway.CreateOrderAndAuthorizeAsync(
            new PayPalAuthorizeOrderRequest(orderId, amount, _settings.Currency, payment.AuthorizeRequestId, card, vaultId),
            cancellationToken);

        if (result.Authorization.Amount != amount)
        {
            _logger.LogWarning(
                "Order {0}: PayPal authorized {1} but the order total is {2}.",
                orderId, result.Authorization.Amount, amount);
        }

        payment.MarkAuthorized(result.PayPalOrderId, result.Authorization.AuthorizationId, result.Authorization.ExpiresAt, result.Authorization.Amount);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        return payment;
    }

    public async Task<OrderPayment> FulfilAsync(int orderId, CancellationToken cancellationToken)
    {
        await EnsureOrderExistsAsync(orderId, cancellationToken);
        var payment = await _paymentRepository.FirstOrDefaultAsync(new OrderPaymentByOrderIdSpec(orderId), cancellationToken)
            ?? throw new OrderPaymentStateException($"Order {orderId} has not been paid yet.");

        if (payment.Status is OrderPaymentStatus.Captured or OrderPaymentStatus.PartiallyRefunded or OrderPaymentStatus.Refunded)
        {
            return payment;
        }

        if (payment.Status != OrderPaymentStatus.Authorized)
        {
            throw new OrderPaymentStateException($"Cannot fulfil order {orderId}: payment status is {payment.Status}, expected {OrderPaymentStatus.Authorized}.");
        }

        var claimed = await _claimStore.TryClaimAsync(ClaimKey(orderId, "capture"), cancellationToken);
        if (!claimed)
        {
            return await _paymentRepository.FirstOrDefaultAsync(new OrderPaymentByOrderIdSpec(orderId), cancellationToken) ?? payment;
        }

        var reauthorizedThisPass = false;

        async Task RenewAsync()
        {
            var renewed = await _gateway.ReauthorizeAsync(payment.AuthorizationId!, payment.AuthorizedAmount, payment.CurrencyCode, payment.AuthorizeRequestId, cancellationToken);
            payment.RenewAuthorization(renewed.AuthorizationId, renewed.ExpiresAt);
            await _paymentRepository.UpdateAsync(payment, cancellationToken);
            reauthorizedThisPass = true;
        }

        if (payment.AuthorizationExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
        {
            await RenewAsync();
        }

        PayPalCaptureResult capture;
        try
        {
            capture = await _gateway.CaptureAsync(payment.AuthorizationId!, payment.CaptureRequestId, cancellationToken);
        }
        catch (PaymentGatewayException ex) when (!reauthorizedThisPass && LooksLikeExpiredAuthorization(ex))
        {
            await RenewAsync();
            capture = await _gateway.CaptureAsync(payment.AuthorizationId!, payment.CaptureRequestId, cancellationToken);
        }

        payment.MarkCaptured(capture.CaptureId, capture.GrossAmount, capture.FeeAmount ?? 0m, capture.NetAmount ?? 0m);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        return payment;
    }

    public async Task<OrderPayment> CancelAsync(int orderId, CancellationToken cancellationToken)
    {
        await EnsureOrderExistsAsync(orderId, cancellationToken);
        var payment = await _paymentRepository.FirstOrDefaultAsync(new OrderPaymentByOrderIdSpec(orderId), cancellationToken)
            ?? throw new OrderPaymentStateException($"Order {orderId} has not been paid yet.");

        if (payment.Status == OrderPaymentStatus.Cancelled)
        {
            return payment;
        }

        if (payment.Status != OrderPaymentStatus.Authorized)
        {
            throw new OrderPaymentStateException($"Cannot cancel order {orderId}: payment status is {payment.Status}, expected {OrderPaymentStatus.Authorized}.");
        }

        var claimed = await _claimStore.TryClaimAsync(ClaimKey(orderId, "void"), cancellationToken);
        if (!claimed)
        {
            return await _paymentRepository.FirstOrDefaultAsync(new OrderPaymentByOrderIdSpec(orderId), cancellationToken) ?? payment;
        }

        await _gateway.VoidAsync(payment.AuthorizationId!, payment.VoidRequestId, cancellationToken);
        payment.MarkCancelled();
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        return payment;
    }

    public async Task<(OrderPayment Payment, OrderRefund Refund)> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        await LoadOwnedOrderAsync(orderId, buyerId, cancellationToken);
        var payment = await _paymentRepository.FirstOrDefaultAsync(new OrderPaymentByOrderIdSpec(orderId), cancellationToken)
            ?? throw new OrderPaymentStateException($"Order {orderId} has not been captured yet.");

        if (payment.Status is not (OrderPaymentStatus.Captured or OrderPaymentStatus.PartiallyRefunded))
        {
            throw new OrderPaymentStateException($"Cannot refund order {orderId}: payment status is {payment.Status}, expected {OrderPaymentStatus.Captured} or {OrderPaymentStatus.PartiallyRefunded}.");
        }

        var capturedAmount = payment.CapturedGrossAmount ?? throw new OrderPaymentStateException($"Order {orderId} has no captured amount on record.");
        var remaining = capturedAmount - payment.TotalRefundedAmount;
        var requestedAmount = amount ?? remaining;

        if (requestedAmount <= 0 || payment.TotalRefundedAmount + requestedAmount > capturedAmount)
        {
            throw new RefundAmountExceededException(
                $"Refund of {requestedAmount} would exceed the captured amount of {capturedAmount} for order {orderId} (already refunded: {payment.TotalRefundedAmount}).");
        }

        var claimKey = ClaimKey(orderId, $"refund:{idempotencyKey}");
        var claimed = await _claimStore.TryClaimAsync(claimKey, cancellationToken);
        if (!claimed)
        {
            var reloaded = await _paymentRepository.FirstOrDefaultAsync(new OrderPaymentByOrderIdSpec(orderId), cancellationToken) ?? payment;
            var existingRefund = reloaded.Refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
            if (existingRefund is not null)
            {
                return (reloaded, existingRefund);
            }
            throw new OrderPaymentStateException($"A refund with idempotency key '{idempotencyKey}' for order {orderId} is already being processed.");
        }

        // Full refund (amount is null) is sent as an empty body to PayPal; a partial refund sends the amount.
        // PayPal's own idempotency key (PayPal-Request-Id) must be unique across this entire merchant
        // account, not just this order - so the claim key (already namespaced by order) is sent to PayPal
        // rather than the caller's raw idempotencyKey, which a different caller/order could plausibly reuse.
        var result = await _gateway.RefundAsync(payment.CaptureId!, amount, payment.CurrencyCode, claimKey, cancellationToken);
        payment.AddRefund(result.RefundId, result.Amount, idempotencyKey, result.Status);
        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        var refund = payment.Refunds.Single(r => r.IdempotencyKey == idempotencyKey);
        return (payment, refund);
    }

    private async Task<Order> LoadOwnedOrderAsync(int orderId, string buyerId, CancellationToken cancellationToken)
    {
        var order = await _orderReadRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), cancellationToken);
        if (order is null || order.BuyerId != buyerId)
        {
            throw new OrderNotFoundException(orderId);
        }
        return order;
    }

    private async Task EnsureOrderExistsAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await _orderReadRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }
    }

    private static string ClaimKey(int orderId, string operation) => $"{orderId}:{operation}";

    private static bool LooksLikeExpiredAuthorization(PaymentGatewayException ex) =>
        ex.Reason == PaymentGatewayFailureReason.ReauthorizationExpired ||
        (ex.Reason == PaymentGatewayFailureReason.ProviderRejected &&
         ex.Message.Contains("AUTHORIZATION", StringComparison.OrdinalIgnoreCase) &&
         ex.Message.Contains("EXPIR", StringComparison.OrdinalIgnoreCase));
}
