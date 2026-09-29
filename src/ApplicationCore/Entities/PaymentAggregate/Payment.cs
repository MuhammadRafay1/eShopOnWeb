using System;
using System.Collections.Generic;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// The money-movement record for a single <see cref="OrderAggregate.Order"/>. One Payment row
/// per order (unique OrderId). Holds enough of the state PayPal owns — the ids and current
/// status of the authorization (hold), the capture, and the refunds — that a later request can
/// act on it, not only the request that created it.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    public int OrderId { get; private set; }
    public string Currency { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    // Authorization (the hold).
    public string PayPalOrderId { get; private set; }
    public string PayPalAuthorizationId { get; private set; }
    public string AuthorizationStatus { get; private set; }
    public DateTimeOffset AuthorizationExpiresAt { get; private set; }
    public int ReauthorizationCount { get; private set; }

    // Capture (money actually taken at fulfilment).
    public string? PayPalCaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }

    public decimal RefundedAmount { get; private set; }

    /// <summary>
    /// An operator-actionable message set when an authorization has gone stale and could not be
    /// renewed. Surfaced on <c>GET /api/my-orders</c> as well as the fulfil response, so it is
    /// visible beyond the single request that produced it. Cleared on a successful capture.
    /// </summary>
    public string? LastOperatorError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    public Payment(int orderId, string currency, decimal amount, string payPalOrderId,
        string payPalAuthorizationId, string authorizationStatus, DateTimeOffset authorizationExpiresAt)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(currency, nameof(currency));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(payPalAuthorizationId, nameof(payPalAuthorizationId));
        Guard.Against.NullOrEmpty(authorizationStatus, nameof(authorizationStatus));

        OrderId = orderId;
        Currency = currency;
        Amount = amount;
        PayPalOrderId = payPalOrderId;
        PayPalAuthorizationId = payPalAuthorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiresAt = authorizationExpiresAt;
        Status = PaymentStatus.Authorized;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Records a renewed (reauthorized) hold, replacing the stale authorization id.</summary>
    public void RenewAuthorization(string newAuthorizationId, string status, DateTimeOffset expiresAt)
    {
        Guard.Against.NullOrEmpty(newAuthorizationId, nameof(newAuthorizationId));
        Guard.Against.NullOrEmpty(status, nameof(status));

        PayPalAuthorizationId = newAuthorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
        ReauthorizationCount++;
        LastOperatorError = null;
    }

    public void RecordRenewalFailure(string message)
    {
        LastOperatorError = message;
    }

    public void Capture(string captureId, string status, decimal capturedAmount, decimal? fee, decimal? net)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        Guard.Against.NullOrEmpty(status, nameof(status));
        Guard.Against.InvalidInput(Status, nameof(Status), s => s == PaymentStatus.Authorized,
            "Only an authorized payment can be captured.");

        PayPalCaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = capturedAmount;
        PayPalFeeAmount = fee;
        NetAmount = net;
        CapturedAt = DateTimeOffset.UtcNow;
        Status = PaymentStatus.Captured;
        LastOperatorError = null;
    }

    public void Void()
    {
        Guard.Against.InvalidInput(Status, nameof(Status), s => s == PaymentStatus.Authorized,
            "Only an authorized payment can be voided.");
        AuthorizationStatus = "VOIDED";
        Status = PaymentStatus.Voided;
    }

    /// <summary>
    /// Appends a refund and advances the refunded total. Guards that the running total can never
    /// exceed the captured amount — this is what makes "a partly-refunded order must never become
    /// refundable beyond what was captured" an invariant enforced in the domain, not just by the
    /// request-validation layer.
    /// </summary>
    public void RecordRefund(Refund refund)
    {
        Guard.Against.Null(refund, nameof(refund));
        Guard.Against.InvalidInput(Status, nameof(Status),
            s => s is PaymentStatus.Captured or PaymentStatus.PartiallyRefunded,
            "Only a captured payment can be refunded.");

        var captured = CapturedAmount ?? 0m;
        Guard.Against.InvalidInput(refund.Amount, nameof(refund.Amount),
            a => RefundedAmount + a <= captured,
            "Refund amount would exceed the captured amount.");

        _refunds.Add(refund);
        RefundedAmount += refund.Amount;
        Status = RefundedAmount >= captured ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
    }

    /// <summary>The amount still available to refund against this capture.</summary>
    public decimal RefundableRemaining => (CapturedAmount ?? 0m) - RefundedAmount;
}
