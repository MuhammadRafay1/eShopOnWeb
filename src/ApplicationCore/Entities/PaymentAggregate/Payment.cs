using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// The payment/fulfilment state for a single Order (1:1). Owns the PayPal ids/statuses needed
/// to act on a later request (renew a stale hold, capture, refund) and the invariants around
/// them. No PAN/CVV is ever stored here.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    public Payment(int orderId, string buyerId, decimal amount, string currencyCode, string invoiceReference)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));
        Guard.Against.NullOrEmpty(invoiceReference, nameof(invoiceReference));

        OrderId = orderId;
        BuyerId = buyerId;
        Amount = amount;
        CurrencyCode = currencyCode;
        InvoiceReference = invoiceReference;
        Status = PaymentStatus.AwaitingPayment;
    }

    public int OrderId { get; private set; }
    public string BuyerId { get; private set; }
    public string CurrencyCode { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string InvoiceReference { get; private set; }

    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    public string? CardBrand { get; private set; }
    public string? CardLast4 { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    public decimal TotalRefunded() => _refunds.Sum(r => r.Amount);

    public void MarkAuthorized(string payPalOrderId, string authorizationId, string authorizationStatus, DateTimeOffset? expiresAt, string? cardBrand, string? cardLast4)
    {
        if (Status is not (PaymentStatus.AwaitingPayment or PaymentStatus.Authorized or PaymentStatus.Failed))
        {
            throw new PaymentConflictException($"Order {OrderId} cannot be authorized from status {Status}.");
        }

        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiresAt = expiresAt;
        CardBrand = cardBrand;
        CardLast4 = cardLast4;
        Status = PaymentStatus.Authorized;
    }

    public void RenewAuthorization(string newAuthorizationId, string newStatus, DateTimeOffset? expiresAt)
    {
        if (Status != PaymentStatus.Authorized)
        {
            throw new PaymentConflictException($"Order {OrderId} authorization can only be renewed while Authorized (current status {Status}).");
        }

        AuthorizationId = newAuthorizationId;
        AuthorizationStatus = newStatus;
        AuthorizationExpiresAt = expiresAt;
    }

    public void MarkCaptured(string captureId, decimal grossAmount, decimal? payPalFee, decimal? netAmount)
    {
        if (Status != PaymentStatus.Authorized)
        {
            throw new PaymentConflictException($"Order {OrderId} cannot be fulfilled from status {Status}; it must be Authorized first.");
        }

        CaptureId = captureId;
        CaptureStatus = "COMPLETED";
        CapturedAmount = grossAmount;
        PayPalFee = payPalFee;
        NetAmount = netAmount;
        CapturedAt = DateTimeOffset.UtcNow;
        Status = PaymentStatus.Captured;
    }

    public void MarkCancelled()
    {
        if (Status != PaymentStatus.Authorized)
        {
            throw new PaymentConflictException($"Order {OrderId} cannot be cancelled from status {Status}; it must be Authorized (and not yet fulfilled).");
        }

        Status = PaymentStatus.Cancelled;
    }

    public void AddRefund(Refund refund)
    {
        if (Status is not (PaymentStatus.Captured or PaymentStatus.PartiallyRefunded))
        {
            throw new PaymentConflictException($"Order {OrderId} cannot be refunded from status {Status}; it must be Captured first.");
        }

        var captured = CapturedAmount ?? 0m;
        if (TotalRefunded() + refund.Amount > captured)
        {
            throw new PaymentConflictException(
                $"Refund of {refund.Amount} for order {OrderId} would exceed the captured amount of {captured} (already refunded {TotalRefunded()}).");
        }

        _refunds.Add(refund);
        RecomputeStatusAfterRefund();
    }

    private void RecomputeStatusAfterRefund()
    {
        var captured = CapturedAmount ?? 0m;
        Status = TotalRefunded() >= captured ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
    }
}
