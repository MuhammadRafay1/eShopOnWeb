using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// The money-movement state PayPal owns for a single <see cref="OrderAggregate.Order"/>. Kept as a
/// separate aggregate root, referenced by <see cref="OrderId"/> value only (no EF navigation across
/// aggregates), exactly the way Order and Basket are separate in this codebase. Holds no card data.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
    #pragma warning restore CS8618

    public Payment(int orderId, string currency, decimal amount, string payPalOrderId,
        string authorizationId, string authorizationStatus, DateTimeOffset? authorizationExpiresAt)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(currency, nameof(currency));
        Guard.Against.Negative(amount, nameof(amount));
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));

        OrderId = orderId;
        Currency = currency;
        Amount = amount;
        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiresAt = authorizationExpiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public int OrderId { get; private set; }        // FK-by-value only — NOT a navigation on Order.
    public string Currency { get; private set; }
    public decimal Amount { get; private set; }     // the order total this payment secures
    public DateTimeOffset CreatedAt { get; private set; }

    public string PayPalOrderId { get; private set; }
    public string AuthorizationId { get; private set; }       // latest — overwritten on reauthorize
    public string AuthorizationStatus { get; private set; }    // raw PayPal wire string, e.g. "CREATED"
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    private readonly List<PaymentRefund> _refunds = new();
    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();

    public decimal TotalRefunded => _refunds.Sum(r => r.Amount);
    public decimal RemainingRefundable => (CapturedAmount ?? 0m) - TotalRefunded;

    /// <summary>Replace the authorization details after a successful reauthorization.</summary>
    public void RecordReauthorization(string authorizationId, string authorizationStatus, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiresAt = expiresAt;
    }

    public void UpdateAuthorizationStatus(string authorizationStatus)
    {
        AuthorizationStatus = authorizationStatus;
    }

    public void RecordCapture(string captureId, string captureStatus, decimal capturedAmount,
        decimal? payPalFee, decimal? netAmount)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        Guard.Against.Negative(capturedAmount, nameof(capturedAmount));

        CaptureId = captureId;
        CaptureStatus = captureStatus;
        CapturedAmount = capturedAmount;
        PayPalFee = payPalFee;
        NetAmount = netAmount;
        CapturedAt = DateTimeOffset.UtcNow;
    }

    public PaymentRefund AddRefund(string payPalRefundId, decimal amount, string status, string idempotencyKey)
    {
        var refund = new PaymentRefund(payPalRefundId, amount, status, idempotencyKey);
        _refunds.Add(refund);
        return refund;
    }
}
