using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Carries the PayPal-owned state of an order's payment: the PayPal order id, the authorization
/// (the hold), the capture (money taken), and any refunds. Created the first time /pay runs and
/// mutated as the order moves through fulfil / cancel / refund. Field names track the PayPal
/// spec's snake_case JSON names in PascalCase (e.g. PayPalFeeAmount from paypal_fee) so the
/// mapping back to the contract stays obvious.
/// </summary>
public class OrderPayment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private OrderPayment() { }

    public OrderPayment(string payPalOrderId, string currencyCode, decimal authorizedAmount, string? invoiceId = null)
    {
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));
        Guard.Against.Negative(authorizedAmount, nameof(authorizedAmount));

        PayPalOrderId = payPalOrderId;
        CurrencyCode = currencyCode;
        AuthorizedAmount = authorizedAmount;
        InvoiceId = invoiceId;
    }

    public int OrderId { get; private set; }

    /// <summary>PayPal order id (Orders v2).</summary>
    public string PayPalOrderId { get; private set; }

    /// <summary>
    /// The globally-unique invoice_id sent to PayPal (e.g. "eshop-order-42-ab12cd34ef56"). Stored so
    /// reconciliation can match transactions on an exact, collision-free key rather than a bare id.
    /// </summary>
    public string? InvoiceId { get; private set; }

    /// <summary>PayPal authorization id (the hold). May change on reauthorization.</summary>
    public string? PayPalAuthorizationId { get; private set; }

    /// <summary>PayPal authorization_status: CREATED | CAPTURED | DENIED | PARTIALLY_CAPTURED | VOIDED | PENDING | EXPIRED.</summary>
    public string? AuthorizationStatus { get; private set; }

    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    /// <summary>PayPal capture id (money taken at fulfilment).</summary>
    public string? PayPalCaptureId { get; private set; }

    /// <summary>PayPal capture_status: COMPLETED | DECLINED | PENDING | REFUNDED | PARTIALLY_REFUNDED | FAILED.</summary>
    public string? CaptureStatus { get; private set; }

    public string CurrencyCode { get; private set; }

    /// <summary>Amount held at authorization (equals the order total to the cent).</summary>
    public decimal AuthorizedAmount { get; private set; }

    /// <summary>gross_amount from seller_receivable_breakdown: the captured amount.</summary>
    public decimal? CapturedAmount { get; private set; }

    /// <summary>paypal_fee from seller_receivable_breakdown.</summary>
    public decimal? PayPalFeeAmount { get; private set; }

    /// <summary>net_amount from seller_receivable_breakdown: net proceeds to the merchant.</summary>
    public decimal? NetAmount { get; private set; }

    /// <summary>Running total of all completed refunds against the capture.</summary>
    public decimal RefundedAmount { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    public void RecordAuthorization(string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        Guard.Against.NullOrEmpty(status, nameof(status));
        PayPalAuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    /// <summary>
    /// Overwrite the authorization id/status/expiry after a reauthorization. PayPal may or may
    /// not return a new id, so always take the values from the response rather than assuming.
    /// </summary>
    public void RecordReauthorization(string authorizationId, string status, DateTimeOffset? expiresAt)
        => RecordAuthorization(authorizationId, status, expiresAt);

    public void RecordCapture(string captureId, string status, decimal capturedAmount, decimal? paypalFee, decimal? netAmount)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        Guard.Against.NullOrEmpty(status, nameof(status));
        PayPalCaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = capturedAmount;
        PayPalFeeAmount = paypalFee;
        NetAmount = netAmount;
    }

    public void RecordVoid()
    {
        AuthorizationStatus = "VOIDED";
    }

    /// <summary>
    /// Records a refund against the capture. Guards that the running refunded total can never
    /// exceed the captured amount — this is the domain-layer enforcement of "a partly-refunded
    /// order must never become refundable beyond what was captured".
    /// </summary>
    public void AddRefund(Refund refund)
    {
        Guard.Against.Null(refund, nameof(refund));
        if (CapturedAmount is null)
        {
            throw new InvalidOperationException("Cannot refund an order that has not been captured.");
        }

        // Only completed/pending refunds count toward the captured ceiling; a FAILED/CANCELLED
        // refund moved no money.
        var moved = refund.Status is "FAILED" or "CANCELLED" ? 0m : refund.Amount;
        if (RefundedAmount + moved > CapturedAmount.Value)
        {
            throw new InvalidOperationException(
                $"Refund of {refund.Amount} would exceed the remaining refundable amount " +
                $"({CapturedAmount.Value - RefundedAmount}).");
        }

        _refunds.Add(refund);
        RefundedAmount += moved;
    }

    /// <summary>Refundable amount remaining on the capture.</summary>
    public decimal RemainingRefundable => (CapturedAmount ?? 0m) - RefundedAmount;

    /// <summary>True when the running refunded total equals the full captured amount.</summary>
    public bool IsFullyRefunded => CapturedAmount is not null && RefundedAmount >= CapturedAmount.Value && CapturedAmount.Value > 0m;
}
