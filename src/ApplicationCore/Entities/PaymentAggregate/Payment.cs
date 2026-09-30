using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// One payment per order (1:1). Created the first time <c>/pay</c> is invoked and then
/// carries the state PayPal owns — the authorization, the capture, and any refunds —
/// so later requests (fulfil, cancel, refund, reconciliation) can act on it.
///
/// PayPal's own resource status strings (CREATED / COMPLETED / VOIDED / ...) are stored
/// verbatim rather than remapped to a local enum, so the two can never drift apart.
/// No raw PAN or CVV is ever stored here or anywhere else in the app.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    private readonly List<Refund> _refunds = new();

    public int OrderId { get; private set; }
    public string CurrencyCode { get; private set; }

    /// <summary>The order total captured at authorization time.</summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Generated once and reused as the PayPal-Request-Id on authorize retries, so a
    /// lost response / double-click cannot authorize the shopper twice.
    /// </summary>
    public Guid IdempotencyKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // --- Authorization (the hold) ---
    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    // --- Capture (money actually taken at fulfilment) ---
    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    public Payment(int orderId, decimal amount, string currencyCode)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.Negative(amount, nameof(amount));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));

        OrderId = orderId;
        Amount = amount;
        CurrencyCode = currencyCode;
        IdempotencyKey = Guid.NewGuid();
    }

    /// <summary>True once PayPal has confirmed a live authorization hold.</summary>
    public bool IsAuthorized => !string.IsNullOrEmpty(AuthorizationId);

    public void SetAuthorization(string payPalOrderId, string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));

        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    /// <summary>Refresh the authorization state (e.g. after a re-authorization or a status re-read).</summary>
    public void UpdateAuthorization(string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    public void SetCapture(string captureId, string status, decimal capturedAmount, decimal? paypalFee, decimal? netAmount)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        CaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = capturedAmount;
        PayPalFeeAmount = paypalFee;
        NetAmount = netAmount;
        CapturedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Total refunded so far across all COMPLETED / PENDING refunds (i.e. everything not failed/cancelled).</summary>
    public decimal TotalRefunded() =>
        _refunds.Where(r => !string.Equals(r.Status, "FAILED", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(r.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r.Amount);

    /// <summary>Amount still available to refund against the capture.</summary>
    public decimal RefundableRemaining() => (CapturedAmount ?? 0m) - TotalRefunded();

    public Refund? FindRefundByIdempotencyKey(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);

    public Refund AddRefund(string payPalRefundId, decimal amount, string status, string idempotencyKey)
    {
        var refund = new Refund(Id, payPalRefundId, amount, status, idempotencyKey);
        _refunds.Add(refund);
        return refund;
    }
}
