using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates the order money movement against <see cref="IPaymentGateway"/>. Each action is separately
/// invocable and idempotent in effect: a per-order in-process lock plus a local status check prevents a
/// double-click from authorizing/capturing twice, and the gateway additionally sends a deterministic
/// PayPal-Request-Id so the provider dedups across processes.
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Buyer> _buyerRepository;
    private readonly IPaymentGateway _gateway;
    private readonly ICurrencyProvider _currency;
    private readonly IOperationLock _lock;
    private readonly IAppLogger<PaymentService> _logger;

    public PaymentService(
        IRepository<Order> orderRepository,
        IRepository<Buyer> buyerRepository,
        IPaymentGateway gateway,
        ICurrencyProvider currency,
        IOperationLock operationLock,
        IAppLogger<PaymentService> logger)
    {
        _orderRepository = orderRepository;
        _buyerRepository = buyerRepository;
        _gateway = gateway;
        _currency = currency;
        _lock = operationLock;
        _logger = logger;
    }

    private static string LockKey(int orderId) => $"order-{orderId}";
    private static string OrderReference(int orderId) => $"order-{orderId}";

    public async Task<Order> AuthorizeAsync(int orderId, string buyerId, CardDetails? card, int? paymentMethodId, CancellationToken cancellationToken)
    {
        using var _ = await _lock.AcquireAsync(LockKey(orderId), cancellationToken);

        var order = await LoadOrderAsync(orderId, cancellationToken);
        if (!string.Equals(order.BuyerId, buyerId, StringComparison.Ordinal))
        {
            throw new OrderNotFoundException(orderId); // never reveal another shopper's order
        }

        if (order.Status == OrderStatus.Authorized)
        {
            return order; // idempotent: already held, do not charge again
        }
        if (order.Status != OrderStatus.AwaitingPayment)
        {
            throw new InvalidPaymentStateException($"Order {orderId} cannot be paid from status '{order.Status}'.");
        }

        var (resolvedCard, vaultTokenId) = await ResolvePaymentSourceAsync(buyerId, card, paymentMethodId, cancellationToken);

        var amount = order.Total();
        if (amount <= 0m)
        {
            throw new BadPaymentRequestException("Order total must be greater than zero to take a payment.");
        }
        var money = new Money(amount, _currency.CurrencyCode);

        var request = new PaymentAuthorizationRequest(OrderReference(orderId), money, resolvedCard, vaultTokenId);
        var result = await _gateway.AuthorizeAsync(request, cancellationToken);

        var payment = new Payment(order.Id, amount, _currency.CurrencyCode);
        payment.SetAuthorized(result.PayPalOrderId, result.AuthorizationId, result.AuthorizationStatus, result.ExpiresAt);
        order.AttachPayment(payment);
        order.MarkAuthorized();
        await _orderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation("Authorized order {0}: paypalOrderId={1} authorizationId={2}", orderId, result.PayPalOrderId, result.AuthorizationId);
        return order;
    }

    public async Task<Order> FulfilAsync(int orderId, CancellationToken cancellationToken)
    {
        using var _ = await _lock.AcquireAsync(LockKey(orderId), cancellationToken);

        var order = await LoadOrderAsync(orderId, cancellationToken);
        if (order.Status == OrderStatus.Fulfilled)
        {
            return order; // idempotent
        }
        if (order.Status != OrderStatus.Authorized || order.Payment is null)
        {
            throw new InvalidPaymentStateException($"Order {orderId} cannot be fulfilled from status '{order.Status}'.");
        }

        var payment = order.Payment;
        var money = new Money(payment.Amount, payment.CurrencyCode);
        var reference = OrderReference(orderId);
        var authorizationId = payment.PayPalAuthorizationId!;

        // Renew proactively if the hold has already lapsed.
        if (IsAuthorizationStale(payment))
        {
            authorizationId = await RenewAuthorizationAsync(order, payment, reference, money, cancellationToken);
        }

        CaptureResult capture;
        try
        {
            capture = await _gateway.CaptureAsync(reference, authorizationId, money, cancellationToken);
        }
        catch (AuthorizationExpiredException)
        {
            // Lapsed between the staleness check and the capture — renew once and retry.
            authorizationId = await RenewAuthorizationAsync(order, payment, reference, money, cancellationToken);
            capture = await _gateway.CaptureAsync(reference, authorizationId, money, cancellationToken);
        }

        payment.SetCaptured(capture.CaptureId, capture.CaptureStatus, capture.CapturedAmount, capture.FeeAmount, capture.NetAmount);
        order.MarkFulfilled();
        await _orderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation("Fulfilled order {0}: captureId={1} captured={2} fee={3} net={4}",
            orderId, capture.CaptureId, capture.CapturedAmount, capture.FeeAmount, capture.NetAmount);
        return order;
    }

    public async Task<Order> CancelAsync(int orderId, CancellationToken cancellationToken)
    {
        using var _ = await _lock.AcquireAsync(LockKey(orderId), cancellationToken);

        var order = await LoadOrderAsync(orderId, cancellationToken);
        if (order.Status == OrderStatus.Cancelled)
        {
            return order; // idempotent
        }
        if (order.Status is not (OrderStatus.AwaitingPayment or OrderStatus.Authorized))
        {
            throw new InvalidPaymentStateException($"Order {orderId} cannot be cancelled from status '{order.Status}'.");
        }

        var payment = order.Payment;
        if (payment is not null && payment.PayPalAuthorizationId is not null && order.Status == OrderStatus.Authorized)
        {
            await _gateway.VoidAsync(OrderReference(orderId), payment.PayPalAuthorizationId, cancellationToken);
            payment.SetVoided();
        }

        order.MarkCancelled();
        await _orderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation("Cancelled order {0}.", orderId);
        return order;
    }

    public async Task<Refund> RefundAsync(int orderId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BadPaymentRequestException("A refund requires an idempotency key.");
        }

        using var _ = await _lock.AcquireAsync(LockKey(orderId), cancellationToken);

        var order = await LoadOrderAsync(orderId, cancellationToken);
        var payment = order.Payment;
        if (payment is null || payment.PayPalCaptureId is null ||
            payment.Status is not (PaymentStatus.Captured or PaymentStatus.PartiallyRefunded))
        {
            throw new InvalidPaymentStateException($"Order {orderId} cannot be refunded from status '{order.Status}'.");
        }

        // Idempotency: a completed refund under this key is returned as-is; no second refund.
        var existing = payment.Refunds.FirstOrDefault(r => string.Equals(r.IdempotencyKey, idempotencyKey, StringComparison.Ordinal));
        if (existing is not null && existing.PayPalRefundId is not null)
        {
            return existing;
        }

        var remaining = payment.RefundableRemaining();
        var requested = amount ?? remaining;
        if (requested <= 0m)
        {
            throw new RefundValidationException("There is nothing left to refund on this order.");
        }
        if (requested > remaining)
        {
            throw new RefundValidationException(
                $"A refund of {requested} {payment.CurrencyCode} exceeds the refundable remainder of {remaining} {payment.CurrencyCode}.");
        }

        var refund = existing ?? new Refund(idempotencyKey, requested, payment.CurrencyCode);
        if (existing is null)
        {
            payment.AddRefund(refund); // PENDING claim row
            await _orderRepository.UpdateAsync(order, cancellationToken);
        }

        var result = await _gateway.RefundAsync(payment.PayPalCaptureId, new Money(requested, payment.CurrencyCode), idempotencyKey, cancellationToken);
        refund.RecordResult(result.RefundId, result.Status);
        payment.RecomputeRefundStatus();
        SyncOrderRefundStatus(order, payment);
        await _orderRepository.UpdateAsync(order, cancellationToken);

        _logger.LogInformation("Refunded order {0}: refundId={1} amount={2} status={3}", orderId, result.RefundId, requested, result.Status);
        return refund;
    }

    private async Task<Order> LoadOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpecification(orderId), cancellationToken);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }
        return order;
    }

    private async Task<(CardDetails? card, string? vaultTokenId)> ResolvePaymentSourceAsync(
        string buyerId, CardDetails? card, int? paymentMethodId, CancellationToken cancellationToken)
    {
        if (paymentMethodId is not null && card is not null)
        {
            throw new BadPaymentRequestException("Supply either card details or a saved paymentMethodId, not both.");
        }

        if (paymentMethodId is not null)
        {
            var buyer = await _buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(buyerId), cancellationToken);
            var method = buyer?.PaymentMethods.FirstOrDefault(m => m.Id == paymentMethodId.Value && !m.IsDeleted);
            if (method?.CardId is null)
            {
                throw new PaymentMethodNotFoundException(paymentMethodId.Value);
            }
            return (null, method.CardId);
        }

        if (card is null)
        {
            throw new BadPaymentRequestException("Supply card details or a saved paymentMethodId to pay.");
        }
        return (card, null);
    }

    private static bool IsAuthorizationStale(Payment payment) =>
        payment.AuthorizationExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow.AddMinutes(1);

    private async Task<string> RenewAuthorizationAsync(Order order, Payment payment, string reference, Money money, CancellationToken cancellationToken)
    {
        var reauth = await _gateway.ReauthorizeAsync(reference, payment.PayPalAuthorizationId!, money, cancellationToken);
        payment.UpdateAuthorization(reauth.AuthorizationId, reauth.AuthorizationStatus, reauth.ExpiresAt);
        await _orderRepository.UpdateAsync(order, cancellationToken);
        return reauth.AuthorizationId;
    }

    private static void SyncOrderRefundStatus(Order order, Payment payment)
    {
        switch (payment.Status)
        {
            case PaymentStatus.Refunded:
                order.MarkRefunded();
                break;
            case PaymentStatus.PartiallyRefunded:
                order.MarkPartiallyRefunded();
                break;
        }
    }
}
