using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// Holds everything PayPal owns about the money movement for one Order: the ids and current
/// status of the authorization (hold), the capture, and any refunds. Kept as its own aggregate,
/// one per Order, so later requests (fulfil, cancel, refund, reconciliation) can act on
/// PayPal-issued state without re-deriving it.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    private readonly List<Refund> _refunds = new();

    #pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    public Payment(int orderId, string currency)
    {
        OrderId = orderId;
        Currency = currency;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public int OrderId { get; private set; }
    public string Currency { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public string? PaypalOrderId { get; private set; }
    public string? InvoiceId { get; private set; }

    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }
    public decimal AuthorizedAmount { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }

    public decimal RefundedTotal { get; private set; }

    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    public decimal RemainingRefundable => (CapturedAmount ?? 0m) - RefundedTotal;

    public void RecordAuthorization(string paypalOrderId, string invoiceId, string authorizationId, string status, DateTimeOffset? expiresAt, decimal amount)
    {
        PaypalOrderId = paypalOrderId;
        InvoiceId = invoiceId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
        AuthorizedAmount = amount;
    }

    public void RecordReauthorization(string authorizationId, string status, DateTimeOffset? expiresAt, decimal amount)
    {
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
        AuthorizedAmount = amount;
    }

    public void RecordCapture(string captureId, string status, decimal captured, decimal? fee, decimal? net)
    {
        CaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = captured;
        PayPalFee = fee;
        NetAmount = net;
    }

    public void RecordVoid()
    {
        AuthorizationStatus = "VOIDED";
    }

    public Refund? FindRefundByKey(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);

    /// <summary>
    /// Records a refund PayPal already performed. <paramref name="runningTotalRefunded"/> is
    /// PayPal's own authoritative total-refunded-to-date for the capture (not re-derived here)
    /// so this can never drift from what PayPal actually holds.
    /// </summary>
    public Refund AddRefund(string refundId, decimal amount, string status, string idempotencyKey, decimal runningTotalRefunded)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Refund amount must be positive.");
        if (runningTotalRefunded > (CapturedAmount ?? 0m) + 0.005m)
            throw new OverRefundException(OrderId, CapturedAmount ?? 0m, RefundedTotal, amount);

        var refund = new Refund(refundId, amount, status, idempotencyKey, DateTimeOffset.UtcNow);
        _refunds.Add(refund);
        RefundedTotal = runningTotalRefunded;
        return refund;
    }
}
