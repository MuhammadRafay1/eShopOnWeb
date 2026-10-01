using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// The payment that belongs to one <see cref="Order"/> (1:1). Holds the state PayPal owns — the ids
/// and current status of the hold (authorization), the capture, and the refunds — so a later request
/// (fulfil, cancel, refund) can act on it rather than only the request that started it. Full card
/// details are never stored here; only PayPal-masked data ends up in the application database.
/// </summary>
public class OrderPayment : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private OrderPayment() { }
#pragma warning restore CS8618

    public OrderPayment(string currency, string invoiceReference)
    {
        Currency = currency;
        InvoiceReference = invoiceReference;
    }

    public int OrderId { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token. Bumped on every state transition so a double-click cannot
    /// authorize, capture or cancel twice — the second concurrent save fails the token check.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>The ISO-4217 currency the payment is taken in (from configuration).</summary>
    public string Currency { get; private set; }

    /// <summary>
    /// The <c>invoice_id</c> sent to PayPal on the purchase unit; a stable per-order reference used to
    /// line PayPal's transaction records up against this eShop order during reconciliation.
    /// </summary>
    public string InvoiceReference { get; private set; }

    public string? PayPalOrderId { get; private set; }
    public string? PayPalAuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public decimal? AuthorizedAmount { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public string? PayPalCaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }

    private readonly List<OrderRefund> _refunds = new();
    public IReadOnlyCollection<OrderRefund> Refunds => _refunds.AsReadOnly();

    internal void ApplyAuthorization(string? payPalOrderId, string? authorizationId, string? authorizationStatus, decimal? authorizedAmount, DateTimeOffset? expiresAt)
    {
        PayPalOrderId = payPalOrderId;
        PayPalAuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizedAmount = authorizedAmount;
        AuthorizationExpiresAt = expiresAt;
    }

    internal void ApplyReauthorization(string authorizationId, string? authorizationStatus, decimal? authorizedAmount, DateTimeOffset? expiresAt)
    {
        // Defensive: reauthorization may not keep the same id — always take the response's own id.
        PayPalAuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        if (authorizedAmount is not null) AuthorizedAmount = authorizedAmount;
        AuthorizationExpiresAt = expiresAt;
    }

    internal void ApplyCapture(string captureId, string? captureStatus, decimal capturedAmount, decimal? fee, decimal? net)
    {
        PayPalCaptureId = captureId;
        CaptureStatus = captureStatus;
        CapturedAmount = capturedAmount;
        PayPalFeeAmount = fee;
        NetAmount = net;
    }

    internal void ApplyVoid(string? authorizationStatus)
    {
        AuthorizationStatus = authorizationStatus;
    }

    internal void Touch() => Version++;

    internal void AddRefund(OrderRefund refund) => _refunds.Add(refund);

    /// <summary>The sum of refunds that count against the captured total (completed or pending).</summary>
    public decimal RefundedAmount() => _refunds.Where(r => r.CountsTowardRefundedTotal).Sum(r => r.Amount);

    /// <summary>What remains refundable: never more than was captured, minus prior non-failed refunds.</summary>
    public decimal RefundableRemaining() => (CapturedAmount ?? 0m) - RefundedAmount();
}
