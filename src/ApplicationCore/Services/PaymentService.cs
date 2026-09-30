using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentService : IPaymentService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<SavedCard> _savedCardRepository;
    private readonly IPayPalClient _payPalClient;
    private readonly PayPalSettings _settings;

    // Confirmed live against PayPal sandbox: PayPal-Request-Id caches and replays even a *failed*
    // validation response on retry, ignoring the corrected body. A stable key derived from the
    // order id would therefore let one transient/validation error permanently "poison" that order
    // (every retry would replay the same cached error). So PayPal-Request-Id here is a fresh value
    // per actual attempt, and the real "never twice" guarantee comes from checking Order/Payment
    // state before ever calling PayPal, serialized per order via this in-process lock (this is a
    // single-instance deployment - see the per-host in-memory store note in the environment docs).
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> OrderLocks = new();

    public PaymentService(IRepository<Order> orderRepository, IRepository<SavedCard> savedCardRepository,
        IPayPalClient payPalClient, IOptions<PayPalSettings> settings)
    {
        _orderRepository = orderRepository;
        _savedCardRepository = savedCardRepository;
        _payPalClient = payPalClient;
        _settings = settings.Value;
    }

    private static SemaphoreSlim LockFor(int orderId) => OrderLocks.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));

    public async Task<Order?> AuthorizeAsync(int orderId, string buyerId, PayPalCardDetails? card, int? paymentMethodId,
        CancellationToken ct = default)
    {
        var orderLock = LockFor(orderId);
        await orderLock.WaitAsync(ct);
        try
        {
            var order = await _orderRepository.FirstOrDefaultAsync(new OrderByIdAndBuyerSpec(orderId, buyerId), ct);
            if (order is null) return null;

            if (order.Status == OrderStatus.Authorized)
            {
                // Idempotent: a double-click never authorizes twice.
                return order;
            }
            if (order.Status != OrderStatus.AwaitingPayment)
            {
                throw new PaymentConflictException($"Order {orderId} is {order.Status} and cannot be paid.");
            }

            var amount = order.Total();
            var currency = _settings.Currency;
            var customId = orderId.ToString();
            var invoiceId = UniqueInvoiceId(orderId);
            var idempotencyKey = Guid.NewGuid().ToString("N");

            PayPalAuthorizationResult result;
            if (paymentMethodId is not null)
            {
                var savedCard = await _savedCardRepository.FirstOrDefaultAsync(
                    new SavedCardByIdAndBuyerSpecification(paymentMethodId.Value, buyerId), ct);
                if (savedCard is null)
                {
                    throw new ResourceNotFoundException($"Saved card {paymentMethodId} was not found.");
                }
                result = await _payPalClient.AuthorizeOrderWithVaultAsync(amount, currency, customId, invoiceId,
                    savedCard.PayPalVaultId, idempotencyKey, ct);
            }
            else if (card is not null)
            {
                result = await _payPalClient.AuthorizeOrderWithCardAsync(amount, currency, customId, invoiceId,
                    card, idempotencyKey, ct);
            }
            else
            {
                throw new ArgumentException("Either card details or a paymentMethodId must be supplied.");
            }

            order.AttachAuthorization(result.PayPalOrderId, result.AuthorizationId, result.AuthorizationStatus,
                currency, result.CardBrand, result.CardLast4);
            await _orderRepository.UpdateAsync(order, ct);
            return order;
        }
        finally
        {
            orderLock.Release();
        }
    }

    public async Task<Order?> FulfilAsync(int orderId, CancellationToken ct = default)
    {
        var orderLock = LockFor(orderId);
        await orderLock.WaitAsync(ct);
        try
        {
            var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), ct);
            if (order is null) return null;

            if (order.Status == OrderStatus.Fulfilled)
            {
                // Idempotent: already captured.
                return order;
            }
            if (order.Status != OrderStatus.Authorized || order.Payment is null)
            {
                throw new PaymentConflictException($"Order {orderId} is {order.Status} and cannot be fulfilled.");
            }

            var payment = order.Payment;
            var total = order.Total();
            var currency = payment.Currency;

            PayPalCaptureResult result;
            try
            {
                result = await _payPalClient.CaptureAsync(payment.AuthorizationId, total, currency,
                    UniqueInvoiceId(orderId), Guid.NewGuid().ToString("N"), ct);
            }
            catch (PayPalOperationException ex) when (IsStaleAuthorization(ex))
            {
                PayPalReauthorizationResult reauth;
                try
                {
                    reauth = await _payPalClient.ReauthorizeAsync(payment.AuthorizationId, total, currency, ct);
                }
                catch (PayPalOperationException)
                {
                    throw new AuthorizationRenewalFailedException(
                        $"Authorization for order {orderId} has expired and can no longer be renewed. " +
                        $"Ask the shopper to pay the order again (POST /api/orders/{orderId}/pay) to create a fresh authorization.");
                }

                order.RenewAuthorization(reauth.AuthorizationId, reauth.Status);
                await _orderRepository.UpdateAsync(order, ct);

                result = await _payPalClient.CaptureAsync(reauth.AuthorizationId, total, currency,
                    UniqueInvoiceId(orderId), Guid.NewGuid().ToString("N"), ct);
            }

            order.RecordCapture(result.CaptureId, result.CaptureStatus, result.GrossAmount, result.PayPalFee,
                result.NetAmount, result.CaptureTime);
            await _orderRepository.UpdateAsync(order, ct);
            return order;
        }
        finally
        {
            orderLock.Release();
        }
    }

    public async Task<Order?> CancelAsync(int orderId, CancellationToken ct = default)
    {
        var orderLock = LockFor(orderId);
        await orderLock.WaitAsync(ct);
        try
        {
            var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId), ct);
            if (order is null) return null;

            if (order.Status == OrderStatus.Cancelled)
            {
                // Idempotent.
                return order;
            }
            if (order.Status is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded or OrderStatus.Refunded)
            {
                throw new PaymentConflictException(
                    $"Order {orderId} has already been fulfilled; cancel is not allowed after fulfilment - use refund instead.");
            }

            if (order.Payment is not null)
            {
                await _payPalClient.VoidAsync(order.Payment.AuthorizationId, ct);
            }
            order.Cancel();
            await _orderRepository.UpdateAsync(order, ct);
            return order;
        }
        finally
        {
            orderLock.Release();
        }
    }

    public async Task<PaymentRefund?> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey,
        CancellationToken ct = default)
    {
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        var orderLock = LockFor(orderId);
        await orderLock.WaitAsync(ct);
        try
        {
            var order = await _orderRepository.FirstOrDefaultAsync(new OrderByIdAndBuyerSpec(orderId, buyerId), ct);
            if (order is null) return null;

            var payment = order.Payment;
            if (payment?.CaptureId is null || payment.CapturedAmount is null)
            {
                throw new PaymentConflictException($"Order {orderId} has not been captured; there is nothing to refund.");
            }

            var existing = payment.FindRefundByIdempotencyKey(idempotencyKey);
            if (existing is not null)
            {
                // Idempotent: repeating the same caller key never refunds twice - this check runs
                // before PayPal is ever called again, so a prior PayPal-side transient failure can't
                // block a legitimate retry under the same key.
                return existing;
            }

            var remaining = payment.CapturedAmount.Value - payment.TotalRefunded();
            var requestedAmount = amount ?? remaining;
            if (requestedAmount <= 0 || requestedAmount > remaining)
            {
                throw new RefundExceedsCapturedAmountException(
                    $"Requested refund of {requestedAmount:F2} exceeds the {remaining:F2} remaining on order {orderId}'s capture.");
            }

            var result = await _payPalClient.RefundAsync(payment.CaptureId, requestedAmount, payment.Currency,
                orderId.ToString(), UniqueInvoiceId(orderId), Guid.NewGuid().ToString("N"), ct);

            var refund = new PaymentRefund(result.RefundId, requestedAmount, result.Status, idempotencyKey);
            order.RecordRefund(refund);
            await _orderRepository.UpdateAsync(order, ct);
            return refund;
        }
        finally
        {
            orderLock.Release();
        }
    }

    // invoice_id only needs to be unique per PayPal call attempt - our own idempotency guarantees
    // come from PayPal-Request-Id plus the pre-checks above, not from a stable invoice_id. A
    // stable "order-{id}" would collide with any prior PayPal activity that reused that
    // reference (e.g. earlier runs against the same sandbox account).
    private static string UniqueInvoiceId(int orderId) => $"order-{orderId}-{Guid.NewGuid():N}";

    private static bool IsStaleAuthorization(PayPalOperationException ex) =>
        ex.ErrorName is "AUTHORIZATION_EXPIRED"
            || ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("honor period", StringComparison.OrdinalIgnoreCase);
}
