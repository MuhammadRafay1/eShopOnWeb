using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Orchestrates the PayPal money movement for an order: authorize (hold) at /pay, capture (take)
/// at /fulfil with a reauthorization fallback for stale holds, void at /cancel, and refund. All the
/// PayPal-side idempotency (deterministic PayPal-Request-Id) lives in the typed clients; this
/// service adds the domain-state and ownership guards on top.
/// </summary>
public class OrderPaymentService : IOrderPaymentService
{
    // PayPal issue codes that indicate a hold has gone stale and must be reauthorized before capture.
    private static readonly string[] StaleAuthorizationIssues =
    {
        "AUTHORIZATION_EXPIRED", "AUTH_EXPIRED", "PAYMENT_EXPIRED"
    };

    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Buyer> _buyerRepository;
    private readonly IPayPalOrdersClient _ordersClient;
    private readonly IPayPalPaymentsClient _paymentsClient;
    private readonly IPayPalCurrencyProvider _currencyProvider;
    private readonly IAppLogger<OrderPaymentService> _logger;

    public OrderPaymentService(
        IRepository<Order> orderRepository,
        IRepository<Buyer> buyerRepository,
        IPayPalOrdersClient ordersClient,
        IPayPalPaymentsClient paymentsClient,
        IPayPalCurrencyProvider currencyProvider,
        IAppLogger<OrderPaymentService> logger)
    {
        _orderRepository = orderRepository;
        _buyerRepository = buyerRepository;
        _ordersClient = ordersClient;
        _paymentsClient = paymentsClient;
        _currencyProvider = currencyProvider;
        _logger = logger;
    }

    public async Task<OrderPayment> AuthorizeAsync(int orderId, string buyerId, PaymentAuthorizationRequest request)
    {
        Guard.Against.Null(request, nameof(request));
        ValidatePaymentSource(request);

        var order = await LoadOwnedOrderAsync(orderId, buyerId);

        // A cancelled order can never be paid.
        if (order.Status == OrderStatus.Cancelled)
        {
            throw new InvalidPaymentRequestException($"Order {orderId} is cancelled and cannot be paid.");
        }

        // Idempotency: once an order has been paid (Authorized or beyond) it already has a hold —
        // never authorize the shopper a second time. Return the existing payment.
        if (order.Status != OrderStatus.AwaitingPayment)
        {
            return order.Payment
                ?? throw new InvalidPaymentRequestException($"Order {orderId} is {order.Status} and cannot be paid.");
        }

        var currency = _currencyProvider.Currency;
        var amount = order.Total();

        var input = new CreateOrderInput
        {
            OrderId = orderId,
            CurrencyCode = currency,
            Amount = amount
        };

        if (request.PaymentMethodId is int paymentMethodId)
        {
            // Re-validate ownership of the saved card at pay time (not just at save time) so a card
            // deleted after saving can no longer be used to pay.
            input.VaultId = await ResolveVaultIdAsync(buyerId, paymentMethodId);
        }
        else
        {
            input.Card = request.Card;
        }

        var createResult = await _ordersClient.CreateOrderAsync(input);
        if (createResult.PayerActionRequired)
        {
            throw new PayPalPayerActionRequiredException(createResult.Id);
        }

        // PayPal may have produced the authorization during create (single-step); otherwise place
        // the hold with an explicit authorize call.
        var authorization = createResult.Authorization
            ?? await _ordersClient.AuthorizeOrderAsync(orderId, createResult.Id);

        var payment = new OrderPayment(createResult.Id, currency, amount, createResult.InvoiceId);
        payment.RecordAuthorization(authorization.Id, authorization.Status, authorization.ExpirationTime);

        order.BeginPayment(payment);
        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation("Order {0} authorized: paypalOrder={1} authorization={2} status={3}",
            orderId, createResult.Id, authorization.Id, authorization.Status);

        return payment;
    }

    public async Task<OrderPayment> FulfilAsync(int orderId)
    {
        var order = await LoadOrderAsync(orderId);

        // Idempotent: capturing an already-fulfilled (or refunded) order does nothing.
        if (order.Status is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded or OrderStatus.Refunded)
        {
            return order.Payment!;
        }
        if (order.Status != OrderStatus.Authorized || order.Payment?.PayPalAuthorizationId is null)
        {
            throw new InvalidPaymentRequestException(
                $"Order {orderId} is {order.Status} and has no authorization to capture.");
        }

        var payment = order.Payment;
        var currency = payment.CurrencyCode;
        var amount = payment.AuthorizedAmount;

        PayPalCaptureResult capture;
        try
        {
            capture = await _paymentsClient.CaptureAuthorizationAsync(
                orderId, payment.PayPalAuthorizationId!, amount, currency);
        }
        catch (PayPalApiException ex) when (IsStaleAuthorization(ex))
        {
            _logger.LogWarning("Order {0} authorization {1} is stale ({2}); attempting reauthorization.",
                orderId, payment.PayPalAuthorizationId!, ex.PayPalName);
            capture = await ReauthorizeAndCaptureAsync(orderId, payment, amount, currency);
        }

        payment.RecordCapture(capture.Id, capture.Status, capture.GrossAmount, capture.PayPalFeeAmount, capture.NetAmount);
        order.MarkFulfilled();
        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation("Order {0} fulfilled: capture={1} gross={2} fee={3} net={4}",
            orderId, capture.Id, capture.GrossAmount, capture.PayPalFeeAmount, capture.NetAmount);

        return payment;
    }

    private async Task<PayPalCaptureResult> ReauthorizeAndCaptureAsync(
        int orderId, OrderPayment payment, decimal amount, string currency)
    {
        PayPalAuthorizationResult reauthorized;
        try
        {
            reauthorized = await _paymentsClient.ReauthorizeAuthorizationAsync(
                orderId, payment.PayPalAuthorizationId!, amount, currency);
        }
        catch (PayPalApiException ex)
        {
            // The hold can no longer be renewed (29-day ceiling passed, or already voided/captured).
            // Terminal — surface PayPal's own error so an operator can act on it, and do NOT fulfil.
            throw new OrderFulfilmentException(
                $"Order {orderId}'s payment hold has expired and can no longer be renewed. " +
                $"Cancel the order and have the shopper pay again. (PayPal: {ex.Message})",
                ex.PayPalName, ex.Details);
        }

        // The reauthorization may return a new authorization id — always overwrite from the response.
        payment.RecordReauthorization(reauthorized.Id, reauthorized.Status, reauthorized.ExpirationTime);

        return await _paymentsClient.CaptureAuthorizationAsync(orderId, reauthorized.Id, amount, currency);
    }

    public async Task CancelAsync(int orderId)
    {
        var order = await LoadOrderAsync(orderId);

        if (order.Status == OrderStatus.Cancelled)
        {
            return; // idempotent
        }

        // Void the hold if one exists and nothing has been captured yet — releases the funds so no
        // money ever moved.
        var payment = order.Payment;
        if (payment?.PayPalAuthorizationId is not null && payment.PayPalCaptureId is null)
        {
            await _paymentsClient.VoidAuthorizationAsync(orderId, payment.PayPalAuthorizationId);
            payment.RecordVoid();
        }

        order.MarkCancelled();
        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation("Order {0} cancelled.", orderId);
    }

    public async Task<Refund> RefundAsync(int orderId, string buyerId, RefundRequest request)
    {
        Guard.Against.Null(request, nameof(request));
        Guard.Against.NullOrEmpty(request.IdempotencyKey, nameof(request.IdempotencyKey));

        var order = await LoadOwnedOrderAsync(orderId, buyerId);

        var payment = order.Payment;
        if (payment?.PayPalCaptureId is null)
        {
            throw new InvalidPaymentRequestException(
                $"Order {orderId} has not been fulfilled, so there is nothing to refund.");
        }

        // Idempotency: a replay under the same key returns the stored refund and calls PayPal not
        // at all. Two DIFFERENT keys are two legitimate distinct partial refunds.
        var existing = payment.Refunds.FirstOrDefault(r => r.IdempotencyKey == request.IdempotencyKey);
        if (existing is not null)
        {
            return existing;
        }

        var remaining = payment.RemainingRefundable;
        var amount = request.Amount ?? remaining;
        if (amount <= 0m)
        {
            throw new InvalidPaymentRequestException("Refund amount must be greater than zero.");
        }
        if (amount > remaining)
        {
            throw new InvalidPaymentRequestException(
                $"Refund of {amount} exceeds the remaining refundable amount ({remaining}).");
        }

        var requestId = $"refund-{orderId}-{request.IdempotencyKey}";
        var result = await _paymentsClient.RefundCaptureAsync(
            payment.PayPalCaptureId!, amount, payment.CurrencyCode, requestId);

        var refund = new Refund(result.Id, result.Amount, result.CurrencyCode, result.Status, request.IdempotencyKey);
        payment.AddRefund(refund);

        order.ApplyRefund(payment.IsFullyRefunded);
        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation("Order {0} refunded: refund={1} amount={2} status={3} fullyRefunded={4}",
            orderId, result.Id, result.Amount, result.Status, payment.IsFullyRefunded);

        return refund;
    }

    private static void ValidatePaymentSource(PaymentAuthorizationRequest request)
    {
        var hasCard = request.Card is not null;
        var hasSaved = request.PaymentMethodId.HasValue;
        if (hasCard == hasSaved)
        {
            throw new InvalidPaymentRequestException(
                "Provide exactly one of a raw card or a saved payment method id to pay with.");
        }
    }

    private async Task<Order> LoadOrderAsync(int orderId)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
        return order ?? throw new OrderNotFoundException(orderId);
    }

    private async Task<Order> LoadOwnedOrderAsync(int orderId, string buyerId)
    {
        var order = await _orderRepository.FirstOrDefaultAsync(new OrderWithPaymentByIdSpec(orderId));
        // Same exception whether the order is missing or owned by someone else — never leak existence.
        if (order is null || order.BuyerId != buyerId)
        {
            throw new OrderNotFoundException(orderId);
        }
        return order;
    }

    private async Task<string> ResolveVaultIdAsync(string buyerId, int paymentMethodId)
    {
        var buyer = await _buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(buyerId));
        var method = buyer?.PaymentMethods.FirstOrDefault(pm => pm.Id == paymentMethodId);
        if (method?.CardId is null)
        {
            throw new PaymentMethodNotFoundException(paymentMethodId);
        }
        return method.CardId;
    }

    private static bool IsStaleAuthorization(PayPalApiException ex) =>
        ex.HttpStatusCode is 422 or 400 && ex.HasIssue(StaleAuthorizationIssues);
}
