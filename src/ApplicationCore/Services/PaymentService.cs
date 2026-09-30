using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>Orchestrates the pay / fulfil / cancel / refund lifecycle of an order's Payment.</summary>
public class PaymentService : IPaymentService
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<SavedPaymentMethod> _paymentMethodRepository;
    private readonly IPaymentGateway _gateway;

    public PaymentService(
        IRepository<Payment> paymentRepository,
        IRepository<SavedPaymentMethod> paymentMethodRepository,
        IPaymentGateway gateway)
    {
        _paymentRepository = paymentRepository;
        _paymentMethodRepository = paymentMethodRepository;
        _gateway = gateway;
    }

    public async Task<PaymentAuthorizationOutcome?> AuthorizeAsync(int orderId, string buyerId, CardDetails? card, int? paymentMethodId, CancellationToken ct = default)
    {
        if ((card is null) == (paymentMethodId is null))
        {
            throw new RequestValidationException("Supply exactly one of a card or a saved payment method id.");
        }

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), ct);
        if (payment is null || payment.BuyerId != buyerId)
        {
            return null;
        }

        if (payment.Status == PaymentStatus.Authorized)
        {
            // Idempotent double-click: the hold already exists, no PayPal call needed.
            return ToAuthorizationOutcome(payment);
        }

        if (payment.Status != PaymentStatus.AwaitingPayment)
        {
            throw new PaymentStateConflictException($"Order {orderId} is not awaiting payment (current status: {payment.Status}).");
        }

        string? vaultId = null;
        if (paymentMethodId.HasValue)
        {
            var savedCard = await _paymentMethodRepository.GetByIdAsync(paymentMethodId.Value, ct);
            if (savedCard is null || savedCard.BuyerId != buyerId)
            {
                throw new RequestValidationException("The saved payment method does not exist.");
            }

            vaultId = savedCard.VaultId;
        }

        if (string.IsNullOrEmpty(payment.PayRequestKey))
        {
            // Persist the idempotency key BEFORE calling PayPal, so a crash-retry reuses it.
            payment.BeginAuthorization(Guid.NewGuid().ToString("N"));
            await _paymentRepository.UpdateAsync(payment, ct);
        }

        var gatewayRequest = new AuthorizeCardPaymentRequest
        {
            Amount = payment.Amount,
            CurrencyCode = payment.CurrencyCode,
            CorrelationReference = payment.CorrelationReference,
            CustomId = payment.OrderId.ToString(CultureInfo.InvariantCulture),
            Card = card,
            VaultId = vaultId,
            ExistingPayPalOrderId = payment.PayPalOrderId,
            RequestKeyBase = payment.PayRequestKey!
        };

        var result = await _gateway.AuthorizeAsync(gatewayRequest, ct);

        if (string.IsNullOrEmpty(payment.PayPalOrderId))
        {
            payment.SetPayPalOrder(result.PayPalOrderId);
        }

        payment.MarkAuthorized(result.AuthorizationId, result.Status, result.HeldAmount, result.ExpiresAt);
        await _paymentRepository.UpdateAsync(payment, ct);

        return ToAuthorizationOutcome(payment);
    }

    public async Task<PaymentFulfilmentOutcome?> FulfilAsync(int orderId, CancellationToken ct = default)
    {
        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), ct);
        if (payment is null)
        {
            return null;
        }

        if (payment.Status == PaymentStatus.Fulfilled)
        {
            // Idempotent double-click: already captured.
            return ToFulfilmentOutcome(payment);
        }

        if (payment.Status != PaymentStatus.Authorized)
        {
            throw new PaymentStateConflictException($"Order {orderId} is not awaiting fulfilment (current status: {payment.Status}).");
        }

        if (payment.AuthorizationExpiresAt.HasValue && payment.AuthorizationExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            var reauthorization = await _gateway.ReauthorizeAsync(
                payment.AuthorizationId!,
                payment.Amount,
                payment.CurrencyCode,
                $"{payment.PayRequestKey}:reauth:{Guid.NewGuid():N}",
                ct);

            payment.UpdateAuthorization(reauthorization.AuthorizationId, reauthorization.Status, reauthorization.ExpiresAt);
            await _paymentRepository.UpdateAsync(payment, ct);
        }

        if (string.IsNullOrEmpty(payment.CaptureRequestKey))
        {
            payment.BeginCapture(Guid.NewGuid().ToString("N"));
            await _paymentRepository.UpdateAsync(payment, ct);
        }

        var capture = await _gateway.CaptureAsync(payment.AuthorizationId!, payment.CaptureRequestKey!, ct);
        payment.MarkCaptured(capture.CaptureId, capture.Status, capture.CapturedAmount, capture.PayPalFee, capture.NetAmount);
        await _paymentRepository.UpdateAsync(payment, ct);

        return ToFulfilmentOutcome(payment);
    }

    public async Task<PaymentCancellationOutcome?> CancelAsync(int orderId, CancellationToken ct = default)
    {
        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), ct);
        if (payment is null)
        {
            return null;
        }

        if (payment.Status == PaymentStatus.Canceled)
        {
            // Idempotent double-click.
            return new PaymentCancellationOutcome { OrderId = orderId, Status = payment.Status.ToString() };
        }

        if (payment.Status == PaymentStatus.Authorized)
        {
            await _gateway.VoidAsync(payment.AuthorizationId!, $"{payment.PayRequestKey}:void", ct);
        }
        else if (payment.Status != PaymentStatus.AwaitingPayment)
        {
            throw new PaymentStateConflictException($"Order {orderId} has already been captured; use a refund instead (current status: {payment.Status}).");
        }

        payment.MarkVoided();
        await _paymentRepository.UpdateAsync(payment, ct);

        return new PaymentCancellationOutcome { OrderId = orderId, Status = payment.Status.ToString() };
    }

    public async Task<PaymentRefundOutcome?> RefundAsync(int orderId, string buyerId, string idempotencyKey, decimal? amount, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new RequestValidationException("An idempotency key is required to issue a refund.");
        }

        if (amount.HasValue && amount.Value <= 0)
        {
            throw new RequestValidationException("Refund amount must be positive.");
        }

        var payment = await _paymentRepository.FirstOrDefaultAsync(new PaymentByOrderIdSpec(orderId), ct);
        if (payment is null || payment.BuyerId != buyerId)
        {
            return null;
        }

        var existingRefund = payment.FindRefundByKey(idempotencyKey);
        if (existingRefund is not null)
        {
            // Idempotency-key replay: return the refund already issued, no PayPal call.
            return ToRefundOutcome(payment, existingRefund);
        }

        if (payment.Status != PaymentStatus.Fulfilled && payment.Status != PaymentStatus.PartiallyRefunded)
        {
            throw new PaymentStateConflictException($"Order {orderId} has not been captured; nothing to refund (current status: {payment.Status}).");
        }

        var capturedAmount = payment.CapturedAmount ?? 0m;
        var alreadyRefunded = payment.TotalRefunded();
        var requestedAmount = amount ?? (capturedAmount - alreadyRefunded);

        if (requestedAmount <= 0 || alreadyRefunded + requestedAmount > capturedAmount)
        {
            throw new PaymentRejectedException(
                $"Refund of {requestedAmount} {payment.CurrencyCode} would exceed the captured amount of {capturedAmount} {payment.CurrencyCode} (already refunded {alreadyRefunded} {payment.CurrencyCode}).");
        }

        var gatewayResult = await _gateway.RefundAsync(payment.CaptureId!, amount, payment.CurrencyCode, idempotencyKey, ct);
        var refund = payment.AddRefund(gatewayResult.RefundId, gatewayResult.Amount, gatewayResult.Status, idempotencyKey);
        await _paymentRepository.UpdateAsync(payment, ct);

        return ToRefundOutcome(payment, refund);
    }

    public async Task<IReadOnlyList<OrderPaymentSummary>> GetOrdersForBuyerAsync(string buyerId, CancellationToken ct = default)
    {
        var payments = await _paymentRepository.ListAsync(new PaymentsByBuyerSpec(buyerId), ct);

        return payments.Select(p => new OrderPaymentSummary
        {
            OrderId = p.OrderId,
            OrderDate = p.CreatedAt,
            Total = p.Amount,
            CurrencyCode = p.CurrencyCode,
            Status = p.Status.ToString(),
            AuthorizationId = p.AuthorizationId,
            CaptureId = p.CaptureId,
            CapturedAmount = p.CapturedAmount,
            PayPalFee = p.PayPalFee,
            NetAmount = p.NetAmount,
            Refunds = p.Refunds.Select(r => new RefundSummary { RefundId = r.RefundId, Amount = r.Amount, Status = r.Status }).ToList()
        }).ToList();
    }

    private static PaymentAuthorizationOutcome ToAuthorizationOutcome(Payment payment) => new()
    {
        OrderId = payment.OrderId,
        Status = payment.Status.ToString(),
        AuthorizationId = payment.AuthorizationId ?? string.Empty,
        HeldAmount = payment.Amount,
        CurrencyCode = payment.CurrencyCode
    };

    private static PaymentFulfilmentOutcome ToFulfilmentOutcome(Payment payment) => new()
    {
        OrderId = payment.OrderId,
        Status = payment.Status.ToString(),
        CaptureId = payment.CaptureId ?? string.Empty,
        CapturedAmount = payment.CapturedAmount ?? 0m,
        PayPalFee = payment.PayPalFee,
        NetAmount = payment.NetAmount,
        CurrencyCode = payment.CurrencyCode
    };

    private static PaymentRefundOutcome ToRefundOutcome(Payment payment, Refund refund) => new()
    {
        RefundId = refund.RefundId,
        OrderId = payment.OrderId,
        Amount = refund.Amount,
        Status = refund.Status,
        TotalRefunded = payment.TotalRefunded(),
        CapturedAmount = payment.CapturedAmount ?? 0m
    };
}
