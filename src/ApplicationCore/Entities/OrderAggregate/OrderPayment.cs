using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Tracks the PayPal-owned state of an order's payment: the ids and status of the hold (authorization),
/// the capture, and any refunds, so a later request can act on them rather than only the one that
/// started them.
/// </summary>
public class OrderPayment : BaseEntity
{
    public int OrderId { get; private set; }
    public string Currency { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentDescription { get; private set; }

    /// <summary>
    /// A per-order salt that idempotency keys are derived from, instead of this row's own small
    /// integer id — PayPal's idempotency cache is scoped to the whole merchant account, shared across
    /// every deployment that points at it, so a key derived from a small sequential id can collide with
    /// unrelated history on that account. A guid keeps keys unique across deployments while staying
    /// stable across retries of the same order's own attempts.
    /// </summary>
    public string IdempotencySalt { get; private set; }

    public string AuthorizeIdempotencyKey => $"{IdempotencySalt}-authorize";
    public string CaptureIdempotencyKey => $"{IdempotencySalt}-capture";
    public string ReauthorizeIdempotencyKey => $"{IdempotencySalt}-reauthorize";
    public string RefundIdempotencyKey(string callerKey) => $"{IdempotencySalt}-refund-{callerKey}";

    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    public decimal RefundedAmount { get; private set; }
    public string? LastError { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

#pragma warning disable CS8618 // Required by Entity Framework
    private OrderPayment() { }

    public OrderPayment(int orderId, decimal amount, string currency, string paymentDescription)
    {
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        PaymentDescription = paymentDescription;
        IdempotencySalt = Guid.NewGuid().ToString("N");
    }

    /// <summary>Re-salts for a fresh attempt — used when a previous authorization attempt failed and
    /// the shopper is paying again, so the new attempt never replays the failed one's cached outcome.</summary>
    public void ResetForNewAttempt(decimal amount, string currency, string paymentDescription)
    {
        Amount = amount;
        Currency = currency;
        PaymentDescription = paymentDescription;
        IdempotencySalt = Guid.NewGuid().ToString("N");
        PayPalOrderId = null;
        AuthorizationId = null;
        AuthorizationStatus = null;
        LastError = null;
    }

    public void RecordAuthorization(string payPalOrderId, string authorizationId, string authorizationStatus)
    {
        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        LastError = null;
    }

    public void RecordAuthorizationFailure(string error)
    {
        LastError = error;
    }

    public void RecordAuthorizationStatus(string authorizationStatus)
    {
        AuthorizationStatus = authorizationStatus;
    }

    public void RecordCapture(string captureId, string captureStatus, decimal capturedAmount, decimal? fee, decimal? net)
    {
        CaptureId = captureId;
        CaptureStatus = captureStatus;
        CapturedAmount = capturedAmount;
        PayPalFee = fee;
        NetAmount = net;
        CapturedAt = DateTimeOffset.UtcNow;
        LastError = null;
    }

    public void RecordFulfilmentFailure(string error)
    {
        LastError = error;
    }

    public void RecordVoid()
    {
        AuthorizationStatus = "VOIDED";
        LastError = null;
    }

    public Refund? FindRefund(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);

    public Refund AddRefundClaim(string idempotencyKey, decimal amount)
    {
        var refund = new Refund(idempotencyKey, amount, Currency);
        _refunds.Add(refund);
        return refund;
    }

    public void RecordRefundResult(Refund refund, string payPalRefundId, string status, decimal amount)
    {
        refund.RecordResult(payPalRefundId, status);
        RefundedAmount += amount;
        LastError = null;
    }

    public decimal RemainingRefundable() => (CapturedAmount ?? 0m) - RefundedAmount;
}
