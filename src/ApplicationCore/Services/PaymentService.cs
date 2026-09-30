using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Configuration;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentService : IPaymentService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentMethod> _paymentMethodRepository;
    private readonly IPayPalClient _payPalClient;
    private readonly PayPalSettings _payPalSettings;

    public PaymentService(
        IRepository<Order> orderRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentMethod> paymentMethodRepository,
        IPayPalClient payPalClient,
        IOptions<PayPalSettings> payPalSettings)
    {
        _orderRepository = orderRepository;
        _paymentRepository = paymentRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _payPalClient = payPalClient;
        _payPalSettings = payPalSettings.Value;
    }

    public async Task<Payment> AuthorizeAsync(int orderId, string buyerId, PaymentAuthorizeInput input, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(input, nameof(input));
        if ((input.Card is null) == (input.PaymentMethodId is null))
        {
            throw new ArgumentException("Provide exactly one of a card or a saved paymentMethodId.", nameof(input));
        }

        var order = await _orderRepository.FirstOrDefaultAsync(new OrderByIdAndBuyerSpec(orderId, buyerId), cancellationToken);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), cancellationToken);
        if (payment is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        // Idempotency short-circuit: a double-click after a successful authorize returns the
        // same hold instead of creating a second one.
        if (order.PaymentStatus == PaymentStatus.Authorized)
        {
            return payment;
        }
        if (order.PaymentStatus != PaymentStatus.AwaitingPayment && order.PaymentStatus != PaymentStatus.AuthorizationFailed)
        {
            throw new InvalidPaymentTransitionException(order.PaymentStatus.ToString(), "authorize");
        }

        PaymentMethod? paymentMethod = null;
        if (input.PaymentMethodId.HasValue)
        {
            paymentMethod = await _paymentMethodRepository.FirstOrDefaultAsync(
                new PaymentMethodByIdAndBuyerSpec(input.PaymentMethodId.Value, buyerId), cancellationToken);
            if (paymentMethod is null)
            {
                throw new PaymentMethodNotFoundException(input.PaymentMethodId.Value);
            }
        }

        var amount = order.Total();
        var currency = _payPalSettings.Currency;

        PayPalCreateOrderInput createInput;
        if (paymentMethod is not null)
        {
            createInput = new PayPalCreateOrderInput
            {
                OrderId = orderId,
                Amount = amount,
                CurrencyCode = currency,
                VaultId = paymentMethod.PayPalVaultId
            };
        }
        else
        {
            string? existingCustomerId = null;
            if (input.SaveCard)
            {
                var existingMethod = await _paymentMethodRepository.FirstOrDefaultAsync(new PaymentMethodsByBuyerSpec(buyerId), cancellationToken);
                existingCustomerId = existingMethod?.PayPalCustomerId;
            }

            createInput = new PayPalCreateOrderInput
            {
                OrderId = orderId,
                Amount = amount,
                CurrencyCode = currency,
                Card = input.Card,
                SaveCardOnSuccess = input.SaveCard,
                ExistingPayPalCustomerId = existingCustomerId,
                MerchantCustomerId = buyerId
            };
        }

        var requestId = $"order-{orderId}-{payment.IdempotencyNonce}-authorize";
        PayPalOrderResult createResult;
        PayPalAuthorizeResult authorizeResult;
        try
        {
            createResult = await _payPalClient.CreateOrderAsync(createInput, requestId, cancellationToken);
            authorizeResult = await _payPalClient.AuthorizeOrderAsync(createResult.PayPalOrderId, requestId, cancellationToken);
        }
        catch (PayPalApiException)
        {
            order.MarkAuthorizationFailed();
            await _orderRepository.UpdateAsync(order, cancellationToken);
            throw;
        }

        if (authorizeResult.RequiresPayerAction)
        {
            order.MarkAuthorizationFailed();
            await _orderRepository.UpdateAsync(order, cancellationToken);
            throw new PaymentChallengeRequiredException(
                $"PayPal order {authorizeResult.PayPalOrderId} is in status '{authorizeResult.OrderStatus}' and requires the payer to approve it in a browser.");
        }

        if (string.IsNullOrEmpty(authorizeResult.AuthorizationId) ||
            string.Equals(authorizeResult.AuthorizationStatus, "DENIED", StringComparison.OrdinalIgnoreCase))
        {
            order.MarkAuthorizationFailed();
            await _orderRepository.UpdateAsync(order, cancellationToken);
            throw new PaymentDeclinedException($"order status '{authorizeResult.OrderStatus}', authorization status '{authorizeResult.AuthorizationStatus}'");
        }

        payment.SetAuthorization(authorizeResult.PayPalOrderId, authorizeResult.AuthorizationId, authorizeResult.AuthorizationStatus ?? "CREATED", authorizeResult.AuthorizationExpiresAt);
        order.MarkAuthorized();

        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        await _orderRepository.UpdateAsync(order, cancellationToken);

        if (paymentMethod is null && input.SaveCard && !string.IsNullOrEmpty(authorizeResult.VaultId) && !string.IsNullOrEmpty(authorizeResult.VaultCustomerId))
        {
            var savedMethod = new PaymentMethod(
                buyerId,
                authorizeResult.VaultId,
                authorizeResult.VaultCustomerId,
                authorizeResult.CardBrand ?? "UNKNOWN",
                authorizeResult.CardLastDigits ?? string.Empty,
                authorizeResult.CardExpiry ?? string.Empty,
                input.Card!.Name);
            await _paymentMethodRepository.AddAsync(savedMethod, cancellationToken);
        }

        return payment;
    }

    public async Task<Payment> FulfilAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), cancellationToken);
        if (payment is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        if (order.PaymentStatus == PaymentStatus.Captured)
        {
            return payment; // idempotent: a double-click after capture returns the same result
        }
        if (order.PaymentStatus != PaymentStatus.Authorized)
        {
            throw new InvalidPaymentTransitionException(order.PaymentStatus.ToString(), "fulfil");
        }

        var authorizationId = payment.AuthorizationId!;
        var requestId = $"order-{orderId}-{payment.IdempotencyNonce}-capture";
        PayPalCaptureResult captureResult;
        try
        {
            captureResult = await _payPalClient.CaptureAuthorizationAsync(authorizationId, payment.AuthorizedAmount, payment.CurrencyCode, requestId, cancellationToken);
        }
        catch (PayPalApiException ex) when (LooksLikeExpiredAuthorization(ex))
        {
            PayPalAuthorizationStatusResult renewed;
            try
            {
                renewed = await _payPalClient.ReauthorizeAsync(authorizationId, payment.AuthorizedAmount, payment.CurrencyCode, $"order-{orderId}-{payment.IdempotencyNonce}-reauthorize", cancellationToken);
            }
            catch (PayPalApiException reauthorizeEx)
            {
                throw new AuthorizationNotRenewableException(authorizationId, orderId, $"{reauthorizeEx.Name}: {reauthorizeEx.Message}");
            }

            payment.RenewAuthorization(renewed.AuthorizationId, renewed.Status, renewed.ExpiresAt);
            await _paymentRepository.UpdateAsync(payment, cancellationToken);

            captureResult = await _payPalClient.CaptureAuthorizationAsync(renewed.AuthorizationId, payment.AuthorizedAmount, payment.CurrencyCode, $"{requestId}-renewed", cancellationToken);
        }

        payment.SetCapture(captureResult.CaptureId, captureResult.Status, captureResult.GrossAmount, captureResult.FeeAmount, captureResult.NetAmount);
        order.MarkCaptured();

        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        await _orderRepository.UpdateAsync(order, cancellationToken);

        return payment;
    }

    public async Task<Payment> CancelAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), cancellationToken);
        if (payment is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        if (order.PaymentStatus == PaymentStatus.Cancelled)
        {
            return payment; // idempotent
        }
        if (order.PaymentStatus != PaymentStatus.Authorized)
        {
            throw new InvalidPaymentTransitionException(order.PaymentStatus.ToString(), "cancel");
        }

        await _payPalClient.VoidAuthorizationAsync(payment.AuthorizationId!, $"order-{orderId}-{payment.IdempotencyNonce}-cancel", cancellationToken);

        order.MarkCancelled();
        await _orderRepository.UpdateAsync(order, cancellationToken);

        return payment;
    }

    public async Task<PaymentRefundResult> RefundAsync(int orderId, string buyerId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(idempotencyKey, nameof(idempotencyKey));

        var order = await _orderRepository.FirstOrDefaultAsync(new OrderByIdAndBuyerSpec(orderId, buyerId), cancellationToken);
        if (order is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), cancellationToken);
        if (payment is null)
        {
            throw new OrderNotFoundException(orderId);
        }

        // Idempotency: repeating a request under the same key returns the same refund rather
        // than refunding again. A different key is a legitimate, distinct partial refund.
        var existingRefund = payment.FindRefundByIdempotencyKey(idempotencyKey);
        if (existingRefund is not null)
        {
            return ToRefundResult(existingRefund, order, payment);
        }

        if (order.PaymentStatus != PaymentStatus.Captured && order.PaymentStatus != PaymentStatus.PartiallyRefunded)
        {
            throw new InvalidPaymentTransitionException(order.PaymentStatus.ToString(), "refund");
        }

        var remaining = payment.RefundableRemaining();
        var requestedAmount = amount ?? remaining;
        if (requestedAmount <= 0 || requestedAmount > remaining)
        {
            throw new RefundExceedsCapturedException(requestedAmount, remaining);
        }

        var result = await _payPalClient.RefundCaptureAsync(payment.CaptureId!, amount, payment.CurrencyCode, idempotencyKey, cancellationToken);

        var refund = new PaymentRefund(result.RefundId, result.Amount, result.Status, idempotencyKey);
        payment.AddRefund(refund);

        var isFullyRefunded = payment.TotalRefunded() >= (payment.CapturedAmount ?? 0m);
        order.MarkRefunded(isFullyRefunded);

        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        await _orderRepository.UpdateAsync(order, cancellationToken);

        return ToRefundResult(refund, order, payment);
    }

    private static PaymentRefundResult ToRefundResult(PaymentRefund refund, Order order, Payment payment)
    {
        return new PaymentRefundResult
        {
            RefundId = refund.PayPalRefundId,
            Status = refund.Status,
            Amount = refund.Amount,
            OrderPaymentStatus = order.PaymentStatus.ToString(),
            TotalRefunded = payment.TotalRefunded(),
            RefundableRemaining = payment.RefundableRemaining()
        };
    }

    public async Task<PaymentMethod> SavePaymentMethodAsync(string buyerId, PayPalCardInput card, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(card, nameof(card));

        var existingMethod = await _paymentMethodRepository.FirstOrDefaultAsync(new PaymentMethodsByBuyerSpec(buyerId), cancellationToken);
        var requestId = $"vault-{buyerId}-{Guid.NewGuid():N}";

        var result = await _payPalClient.CreatePaymentTokenAsync(card, existingMethod?.PayPalCustomerId, buyerId, requestId, cancellationToken);

        var method = new PaymentMethod(
            buyerId,
            result.VaultId,
            result.PayPalCustomerId,
            result.Brand ?? "UNKNOWN",
            result.LastDigits ?? string.Empty,
            result.Expiry ?? string.Empty,
            card.Name);

        await _paymentMethodRepository.AddAsync(method, cancellationToken);
        return method;
    }

    public async Task DeletePaymentMethodAsync(int paymentMethodId, string buyerId, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var method = await _paymentMethodRepository.FirstOrDefaultAsync(new PaymentMethodByIdAndBuyerSpec(paymentMethodId, buyerId), cancellationToken);
        if (method is null)
        {
            throw new PaymentMethodNotFoundException(paymentMethodId);
        }

        await _payPalClient.DeletePaymentTokenAsync(method.PayPalVaultId, cancellationToken);
        await _paymentMethodRepository.DeleteAsync(method, cancellationToken);
    }

    public async Task<ReconciliationReport> GetReconciliationReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (from > to)
        {
            throw new ArgumentException("'from' must not be after 'to'.");
        }

        var transactions = await _payPalClient.SearchTransactionsAsync(from, to, cancellationToken);
        var capturedPayments = await _paymentRepository.ListAsync(new CapturedPaymentsSpec(), cancellationToken);

        var paymentsByCaptureId = capturedPayments
            .Where(p => !string.IsNullOrEmpty(p.CaptureId))
            .ToDictionary(p => p.CaptureId!, p => p);

        var matched = new List<ReconciliationMatch>();
        var paypalOnly = new List<PayPalTransaction>();
        var matchedCaptureIds = new HashSet<string>();

        foreach (var transaction in transactions)
        {
            if (paymentsByCaptureId.TryGetValue(transaction.TransactionId, out var payment))
            {
                matched.Add(new ReconciliationMatch
                {
                    OrderId = payment.OrderId,
                    TransactionId = transaction.TransactionId,
                    EShopCapturedAmount = payment.CapturedAmount ?? 0m,
                    PayPalAmount = transaction.Amount,
                    PayPalFeeAmount = transaction.FeeAmount,
                    PayPalStatus = transaction.Status
                });
                matchedCaptureIds.Add(transaction.TransactionId);
            }
            else
            {
                paypalOnly.Add(transaction);
            }
        }

        var eShopOnly = capturedPayments
            .Where(p => !string.IsNullOrEmpty(p.CaptureId) && !matchedCaptureIds.Contains(p.CaptureId!))
            .Select(p => new ReconciliationEShopOnlyRow
            {
                OrderId = p.OrderId,
                CaptureId = p.CaptureId!,
                CapturedAmount = p.CapturedAmount ?? 0m
            })
            .ToList();

        return new ReconciliationReport
        {
            From = from,
            To = to,
            Matched = matched,
            PayPalOnly = paypalOnly,
            EShopOnly = eShopOnly
        };
    }

    private static bool LooksLikeExpiredAuthorization(PayPalApiException ex)
    {
        if (ex.StatusCode != System.Net.HttpStatusCode.UnprocessableEntity)
        {
            return false;
        }
        return ex.Details.Any(d => d.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase))
            || ex.Message.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase)
            || (ex.Name?.Contains("EXPIRED", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
