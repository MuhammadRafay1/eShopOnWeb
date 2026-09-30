using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// The money side of an <see cref="Order"/>: one Payment per order, carrying enough of the
/// state PayPal owns (ids and current status for the hold, the capture and the refunds) that a
/// later request can act on it. Child of the Order aggregate.
/// </summary>
public class Payment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    /// <summary>
    /// Created when an order is authorized. Freezes the currency used and records the PayPal
    /// order id plus the authorization (hold) PayPal returned.
    /// </summary>
    public Payment(string currency, string payPalOrderId, string authorizationId,
        string authorizationStatus, decimal authorizedAmount, DateTimeOffset? authorizationExpiresAt,
        string? paymentMethodId)
    {
        Guard.Against.NullOrEmpty(currency, nameof(currency));
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));

        Currency = currency;
        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizedAmount = authorizedAmount;
        AuthorizationExpiresAt = authorizationExpiresAt;
        PaymentMethodId = paymentMethodId;
    }

    public int OrderId { get; private set; }

    /// <summary>ISO-4217 currency, frozen at authorization time from PayPal:Currency.</summary>
    public string Currency { get; private set; }

    /// <summary>The v2 checkout order id from the create-order call.</summary>
    public string PayPalOrderId { get; private set; }

    // ---- Authorization (the hold) ----
    public string AuthorizationId { get; private set; }
    public string AuthorizationStatus { get; private set; }
    public decimal AuthorizedAmount { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    // ---- Capture (money taken at fulfilment) ----
    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    /// <summary>Running total of refunds applied against the capture.</summary>
    public decimal RefundedAmount { get; private set; }

    /// <summary>The saved card (PayPal vault id) used to pay, if any; null for a one-off card.</summary>
    public string? PaymentMethodId { get; private set; }

    private readonly List<Refund> _refunds = new List<Refund>();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    /// <summary>An authorization is capturable while CREATED (not yet captured/voided/denied).</summary>
    public bool IsAuthorizationCapturable =>
        string.Equals(AuthorizationStatus, "CREATED", StringComparison.OrdinalIgnoreCase);

    public bool IsAuthorizationExpired =>
        AuthorizationExpiresAt.HasValue && AuthorizationExpiresAt.Value <= DateTimeOffset.UtcNow;

    /// <summary>Records a fresh authorization after PayPal reauthorized a stale hold.</summary>
    public void RecordReauthorization(string authorizationId, string authorizationStatus,
        decimal authorizedAmount, DateTimeOffset? authorizationExpiresAt)
    {
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizedAmount = authorizedAmount;
        AuthorizationExpiresAt = authorizationExpiresAt;
    }

    /// <summary>Records the capture PayPal reported at fulfilment.</summary>
    public void RecordCapture(string captureId, string captureStatus, decimal capturedAmount,
        decimal? payPalFee, decimal? netAmount, DateTimeOffset capturedAt)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        CaptureId = captureId;
        CaptureStatus = captureStatus;
        CapturedAmount = capturedAmount;
        PayPalFee = payPalFee;
        NetAmount = netAmount;
        CapturedAt = capturedAt;
        AuthorizationStatus = "CAPTURED";
    }

    /// <summary>Marks the hold as released after a void at PayPal.</summary>
    public void MarkVoided()
    {
        AuthorizationStatus = "VOIDED";
    }

    public Refund? FindRefundByIdempotencyKey(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);

    /// <summary>
    /// Adds a refund, enforcing the invariant that total refunds never exceed what was captured.
    /// This guard is the single source of truth for "never refundable beyond what was captured",
    /// regardless of which endpoint calls it.
    /// </summary>
    public Refund AddRefund(string payPalRefundId, decimal amount, string status, string idempotencyKey)
    {
        if (CapturedAmount is null || CaptureId is null)
            throw new OrderStateException("Cannot refund a payment that has not been captured.");

        if (RefundedAmount + amount > CapturedAmount.Value)
            throw new RefundExceedsCaptureException(amount, RefundedAmount, CapturedAmount.Value);

        var refund = new Refund(payPalRefundId, amount, status, idempotencyKey);
        _refunds.Add(refund);
        RefundedAmount += amount;
        CaptureStatus = RefundedAmount >= CapturedAmount.Value ? "REFUNDED" : "PARTIALLY_REFUNDED";
        return refund;
    }

    /// <summary>Remaining amount that can still be refunded against the capture.</summary>
    public decimal RemainingRefundable =>
        (CapturedAmount ?? 0m) - RefundedAmount;
}
