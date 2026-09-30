using System;
using System.Collections.Generic;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// The money-movement record for a single <see cref="Order"/>. Created when the shopper pays
/// (authorize), updated at fulfilment (capture) and on each refund. Mirrors the state PayPal owns
/// (ids + current status for the hold, the capture and the refunds) so later requests can act on it.
/// No card details are ever stored here.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    public Payment(int orderId, string currency, decimal amount, string payPalOrderId,
        string invoiceId, string authorizationId, string authorizationStatus,
        DateTimeOffset? authorizationExpiresAt, string authorizeRequestId, int? paymentMethodId)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(currency, nameof(currency));
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(invoiceId, nameof(invoiceId));
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));

        OrderId = orderId;
        Currency = currency;
        Amount = amount;
        PayPalOrderId = payPalOrderId;
        InvoiceId = invoiceId;
        PayPalAuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiresAt = authorizationExpiresAt;
        AuthorizeRequestId = authorizeRequestId;
        PaymentMethodId = paymentMethodId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        AddEvent(PaymentEventType.Authorized, authorizationId, authorizationStatus);
    }

    public int OrderId { get; private set; }
    public Order Order { get; private set; }

    /// <summary>ISO-4217 currency, snapshot of PayPal:Currency at authorize time.</summary>
    public string Currency { get; private set; }

    /// <summary>The amount PayPal was asked to hold — snapshot of Order.Total() at authorize time.</summary>
    public decimal Amount { get; private set; }

    /// <summary>The invoice_id sent to PayPal (join key for reconciliation).</summary>
    public string InvoiceId { get; private set; }

    // --- Authorization (the hold) ---
    public string PayPalOrderId { get; private set; }
    public string? PayPalAuthorizationId { get; private set; }
    public string AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }
    public string AuthorizeRequestId { get; private set; }

    // --- Capture (funds taken at fulfilment) ---
    public string? PayPalCaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }
    public string? CaptureRequestId { get; private set; }

    // --- Refunds ---
    public decimal RefundedAmount { get; private set; }

    // --- Saved-card linkage ---
    public int? PaymentMethodId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    private readonly List<PaymentEvent> _events = new();
    public IReadOnlyCollection<PaymentEvent> Events => _events.AsReadOnly();

    /// <summary>True once the hold has been captured (money taken).</summary>
    public bool IsCaptured => CaptureStatus is not null && PayPalCaptureId is not null;

    /// <summary>How much of the captured amount can still be refunded.</summary>
    public decimal RemainingRefundable => (CapturedAmount ?? 0m) - RefundedAmount;

    public void RecordReauthorization(string newAuthorizationId, string status, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(newAuthorizationId, nameof(newAuthorizationId));
        PayPalAuthorizationId = newAuthorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
        Touch();
        AddEvent(PaymentEventType.Reauthorized, newAuthorizationId, status);
    }

    public void RecordCapture(string captureId, string status, decimal capturedAmount,
        decimal feeAmount, decimal netAmount, string captureRequestId)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        PayPalCaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = capturedAmount;
        PayPalFeeAmount = feeAmount;
        NetAmount = netAmount;
        CaptureRequestId = captureRequestId;
        Touch();
        AddEvent(PaymentEventType.Captured, captureId, status);
    }

    public void RecordVoid()
    {
        AuthorizationStatus = "VOIDED";
        Touch();
        AddEvent(PaymentEventType.Voided, PayPalAuthorizationId, "VOIDED");
    }

    public Refund RecordRefund(string payPalRefundId, decimal amount, string status, string idempotencyKey)
    {
        var refund = new Refund(Id, payPalRefundId, amount, status, idempotencyKey);
        _refunds.Add(refund);
        RefundedAmount += amount;
        if (CaptureStatus is not null)
        {
            CaptureStatus = RefundedAmount >= (CapturedAmount ?? 0m) ? "REFUNDED" : "PARTIALLY_REFUNDED";
        }
        Touch();
        AddEvent(PaymentEventType.Refunded, payPalRefundId, status);
        return refund;
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private void AddEvent(PaymentEventType type, string? payPalId, string? status)
        => _events.Add(new PaymentEvent(type, payPalId, status));
}
