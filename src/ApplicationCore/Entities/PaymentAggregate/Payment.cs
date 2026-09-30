using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// Tracks PayPal's own state for one order's payment: the hold (authorization), the capture, and
/// any refunds. One Payment per Order (1:1, keyed by OrderId), kept as its own aggregate root
/// because pay/fulfil/refund act on it across separate requests and it needs its own repository.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    public Payment(int orderId, string buyerId, string currency, decimal authorizedAmount)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(currency, nameof(currency));

        OrderId = orderId;
        BuyerId = buyerId;
        Currency = currency;
        AuthorizedAmount = authorizedAmount;
    }

    public int OrderId { get; private set; }
    public string BuyerId { get; private set; }
    public string Currency { get; private set; }
    public decimal AuthorizedAmount { get; private set; }

    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }

    public string? CardBrand { get; private set; }
    public string? CardLast4 { get; private set; }

    public PaymentStatus Status { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    public void RecordAuthorization(string payPalOrderId, string authorizationId, string status, DateTimeOffset? expiresAt, string? cardBrand, string? cardLast4)
    {
        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
        CardBrand = cardBrand ?? CardBrand;
        CardLast4 = cardLast4 ?? CardLast4;
        Status = PaymentStatus.Authorized;
    }

    public void RenewAuthorization(string newAuthorizationId, string status, DateTimeOffset? expiresAt)
    {
        AuthorizationId = newAuthorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    public void RecordCapture(string captureId, string status, decimal capturedAmount, decimal? paypalFee, decimal? netAmount)
    {
        CaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = capturedAmount;
        PayPalFee = paypalFee;
        NetAmount = netAmount;
        Status = PaymentStatus.Captured;
    }

    public void RecordVoid()
    {
        AuthorizationStatus = "VOIDED";
        Status = PaymentStatus.Voided;
    }

    public decimal TotalRefunded() => _refunds.Sum(r => r.Amount);

    public bool CanRefund(decimal amount) =>
        CapturedAmount.HasValue && amount > 0 && TotalRefunded() + amount <= CapturedAmount.Value;

    public Refund? FindRefundByIdempotencyKey(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);

    public Refund AddRefund(string payPalRefundId, decimal amount, string status, string idempotencyKey, string? note)
    {
        if (!CanRefund(amount))
        {
            throw new RefundExceedsCaptureException(
                $"Refund of {amount} {Currency} would exceed the captured amount ({CapturedAmount}) minus refunds already issued ({TotalRefunded()}) for payment {Id}");
        }

        var refund = new Refund(payPalRefundId, amount, status, idempotencyKey, note);
        _refunds.Add(refund);

        Status = TotalRefunded() >= CapturedAmount!.Value ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;

        return refund;
    }
}
