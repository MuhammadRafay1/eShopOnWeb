using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentService : IPaymentService
{
    // A per-process nonce so PayPal-Request-Id values stay unique across app runs (the in-memory
    // DB resets order ids to 1 on restart), while remaining deterministic within a single run —
    // so a double-click in the same run still de-dupes at PayPal. In-run state checks are the
    // primary idempotency guard; this is the secondary belt-and-braces layer.
    private static readonly string RunId = Guid.NewGuid().ToString("N").Substring(0, 8);

    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<PaymentMethod> _paymentMethodRepository;
    private readonly IPayPalOrdersClient _ordersClient;
    private readonly IPayPalPaymentsClient _paymentsClient;
    private readonly PayPalOptions _options;
    private readonly IAppLogger<PaymentService> _logger;

    public PaymentService(
        IRepository<Order> orderRepository,
        IRepository<PaymentMethod> paymentMethodRepository,
        IPayPalOrdersClient ordersClient,
        IPayPalPaymentsClient paymentsClient,
        PayPalOptions options,
        IAppLogger<PaymentService> logger)
    {
        _orderRepository = orderRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _ordersClient = ordersClient;
        _paymentsClient = paymentsClient;
        _options = options;
        _logger = logger;
    }

    public async Task<Order?> AuthorizeAsync(int orderId, string buyerId, PayPalCard? card,
        string? paymentMethodId, CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderForBuyerAsync(orderId, buyerId);
        if (order is null) return null;

        // Idempotent in effect: a double-click never authorizes twice.
        if (order.Status == OrderStatus.PaymentAuthorized || order.Status == OrderStatus.Fulfilled)
        {
            _logger.LogInformation("Order {0} is already {1}; returning existing authorization.",
                orderId, order.Status);
            return order;
        }
        if (order.Status != OrderStatus.AwaitingPayment)
            throw new OrderStateException($"Order {orderId} cannot be paid from state {order.Status}.");

        // Resolve a saved card to its PayPal vault id, scoped to the caller. A card that exists
        // but belongs to someone else simply isn't found here — the API then 404s.
        string? vaultId = null;
        if (!string.IsNullOrWhiteSpace(paymentMethodId))
        {
            var pmSpec = new PaymentMethodByIdForBuyerSpecification(paymentMethodId!, buyerId);
            var savedCard = await _paymentMethodRepository.FirstOrDefaultAsync(pmSpec, cancellationToken);
            if (savedCard is null)
                throw new ResourceNotFoundException(
                    $"Saved card {paymentMethodId} was not found for this shopper.");
            vaultId = savedCard.PayPalVaultId;
        }

        var amount = RoundToCurrency(order.Total());
        var requestId = $"eshop-{RunId}-order-{orderId}-authorize";

        var result = await _ordersClient.CreateAuthorizedOrderAsync(
            amount, orderId.ToString(CultureInfo.InvariantCulture), card, vaultId, requestId,
            cancellationToken);

        // Detect a challenge/3-D Secure requirement and stop, rather than building an approval flow.
        if (IsPayerActionRequired(result))
            throw PaymentActionException.PayerActionRequired();

        if (string.IsNullOrEmpty(result.AuthorizationId) ||
            string.Equals(result.AuthorizationStatus, "DENIED", StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentActionException("authorization_declined",
                $"PayPal did not authorize the payment (order status {result.OrderStatus}, " +
                $"authorization status {result.AuthorizationStatus}).");
        }

        var payment = new Payment(
            currency: result.CurrencyCode,
            payPalOrderId: result.PayPalOrderId,
            authorizationId: result.AuthorizationId,
            authorizationStatus: result.AuthorizationStatus,
            authorizedAmount: result.Amount,
            authorizationExpiresAt: result.ExpiresAt,
            paymentMethodId: vaultId);

        order.MarkPaymentAuthorized(payment);
        await _orderRepository.UpdateAsync(order, cancellationToken);
        _logger.LogInformation("Order {0} authorized (PayPal order {1}, auth {2}).",
            orderId, result.PayPalOrderId, result.AuthorizationId);
        return order;
    }

    public async Task<Order?> FulfilAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        if (order.Status == OrderStatus.Fulfilled)
            return order; // already captured — idempotent

        if (order.Status != OrderStatus.PaymentAuthorized || order.Payment is null)
            throw new OrderStateException(
                $"Order {orderId} cannot be fulfilled from state {order.Status}; it must be PaymentAuthorized.");

        var payment = order.Payment;
        var amount = RoundToCurrency(order.Total());
        var captureAuthId = payment.AuthorizationId;

        // Renew a stale hold rather than failing the fulfilment outright.
        bool needsReauth;
        try
        {
            var current = await _paymentsClient.GetAuthorizationAsync(payment.AuthorizationId, cancellationToken);
            needsReauth = !string.Equals(current.Status, "CREATED", StringComparison.OrdinalIgnoreCase)
                          || (current.ExpiresAt.HasValue && current.ExpiresAt.Value <= DateTimeOffset.UtcNow);
        }
        catch (PayPalApiException ex)
        {
            _logger.LogWarning("Could not read authorization {0} for order {1}: {2}. Will attempt reauthorization.",
                payment.AuthorizationId, orderId, ex.Message);
            needsReauth = true;
        }

        if (needsReauth)
        {
            try
            {
                var reauth = await _paymentsClient.ReauthorizeAsync(
                    payment.AuthorizationId, amount, $"eshop-{RunId}-order-{orderId}-reauthorize", cancellationToken);
                payment.RecordReauthorization(reauth.AuthorizationId, reauth.Status, reauth.Amount, reauth.ExpiresAt);
                captureAuthId = reauth.AuthorizationId;
                _logger.LogInformation("Order {0} authorization renewed as {1}.", orderId, reauth.AuthorizationId);
            }
            catch (PayPalApiException ex)
            {
                // Save the renewal attempt's state but leave the order un-fulfilled.
                await _orderRepository.UpdateAsync(order, cancellationToken);
                throw PaymentActionException.AuthorizationUnrenewable(
                    $"PayPal reported: {ex.Message}");
            }
        }

        var capture = await _paymentsClient.CaptureAuthorizationAsync(
            captureAuthId, amount, $"eshop-{RunId}-order-{orderId}-capture", cancellationToken);

        payment.RecordCapture(capture.CaptureId, capture.Status, capture.GrossAmount,
            capture.PayPalFee, capture.NetAmount, capture.CapturedAt);
        order.MarkFulfilled();
        await _orderRepository.UpdateAsync(order, cancellationToken);
        _logger.LogInformation("Order {0} fulfilled; captured {1} {2}, fee {3}, net {4}.",
            orderId, capture.GrossAmount, capture.CurrencyCode, capture.PayPalFee, capture.NetAmount);
        return order;
    }

    public async Task<Order?> CancelAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null) return null;

        if (order.Status == OrderStatus.Cancelled)
            return order; // idempotent

        if (order.Status == OrderStatus.Fulfilled)
            throw new OrderStateException(
                $"Order {orderId} has been fulfilled and cannot be cancelled; issue a refund instead.");

        // Release any held funds at PayPal before cancelling locally.
        if (order.Status == OrderStatus.PaymentAuthorized && order.Payment is not null)
        {
            var payment = order.Payment;
            if (string.Equals(payment.AuthorizationStatus, "CREATED", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _paymentsClient.VoidAuthorizationAsync(
                        payment.AuthorizationId, $"eshop-{RunId}-order-{orderId}-void", cancellationToken);
                }
                catch (PayPalApiException ex)
                {
                    // If PayPal says it's already voided/expired, the hold is gone anyway.
                    _logger.LogWarning("Void of authorization {0} for order {1} reported: {2}.",
                        payment.AuthorizationId, orderId, ex.Message);
                }
            }
            payment.MarkVoided();
        }

        order.MarkCancelled();
        await _orderRepository.UpdateAsync(order, cancellationToken);
        _logger.LogInformation("Order {0} cancelled.", orderId);
        return order;
    }

    public async Task<Refund?> RefundAsync(int orderId, string buyerId, decimal? amount,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("An idempotency key is required for refunds.", nameof(idempotencyKey));

        var order = await LoadOrderForBuyerAsync(orderId, buyerId);
        if (order is null) return null;

        if (order.Status != OrderStatus.Fulfilled || order.Payment?.CaptureId is null)
            throw new OrderStateException(
                $"Order {orderId} cannot be refunded; it has not been captured.");

        var payment = order.Payment;

        // Idempotent: a repeat under the same key returns the original refund without calling PayPal.
        var existing = payment.FindRefundByIdempotencyKey(idempotencyKey);
        if (existing is not null)
        {
            _logger.LogInformation("Refund idempotency key {0} already applied to order {1}.",
                idempotencyKey, orderId);
            return existing;
        }

        var refundAmount = RoundToCurrency(amount ?? payment.RemainingRefundable);

        // Enforce "never refundable beyond what was captured" locally before calling PayPal.
        if (refundAmount <= 0m || payment.RefundedAmount + refundAmount > payment.CapturedAmount!.Value)
            throw new RefundExceedsCaptureException(refundAmount, payment.RefundedAmount, payment.CapturedAmount!.Value);

        var result = await _paymentsClient.RefundCaptureAsync(
            payment.CaptureId!, refundAmount, orderId.ToString(CultureInfo.InvariantCulture),
            idempotencyKey, cancellationToken);

        var refund = payment.AddRefund(result.RefundId, result.Amount, result.Status, idempotencyKey);
        await _orderRepository.UpdateAsync(order, cancellationToken);
        _logger.LogInformation("Order {0} refunded {1} (refund {2}).", orderId, result.Amount, result.RefundId);
        return refund;
    }

    private async Task<Order?> LoadOrderAsync(int orderId)
    {
        var spec = new OrderWithPaymentByIdSpecification(orderId);
        return await _orderRepository.FirstOrDefaultAsync(spec);
    }

    private async Task<Order?> LoadOrderForBuyerAsync(int orderId, string buyerId)
    {
        var order = await LoadOrderAsync(orderId);
        // 404 (via null), not 403, when the order isn't the caller's — don't confirm it exists.
        if (order is null || !string.Equals(order.BuyerId, buyerId, StringComparison.Ordinal))
            return null;
        return order;
    }

    private static bool IsPayerActionRequired(PayPalAuthorizeResult result) =>
        string.Equals(result.OrderStatus, "PAYER_ACTION_REQUIRED", StringComparison.OrdinalIgnoreCase);

    private decimal RoundToCurrency(decimal amount)
    {
        var decimals = CurrencyDecimals(_options.Currency);
        return Math.Round(amount, decimals, MidpointRounding.AwayFromZero);
    }

    // Most currencies use 2 decimal places; a few use 0 or 3. Kept minimal and spec-agnostic.
    private static int CurrencyDecimals(string currency) => currency?.ToUpperInvariant() switch
    {
        "JPY" or "HUF" or "TWD" => 0,
        _ => 2
    };
}
