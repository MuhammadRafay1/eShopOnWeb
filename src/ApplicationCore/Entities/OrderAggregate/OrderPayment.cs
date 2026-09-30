using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// The PayPal-owned payment state for a single order (1:1 with <see cref="Order"/>). Holds the ids and
/// amounts PayPal reports across the hold → capture → refund lifecycle, so a later request can act on the
/// payment without re-deriving anything. No card data is ever stored here.
/// </summary>
public class OrderPayment : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private OrderPayment() { }

    public OrderPayment(
        int orderId,
        string currency,
        string payPalOrderId,
        string payPalAuthorizationId,
        decimal authorizedAmount,
        DateTimeOffset authorizationExpiresAt,
        int? savedPaymentMethodId)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(currency, nameof(currency));
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(payPalAuthorizationId, nameof(payPalAuthorizationId));

        OrderId = orderId;
        Currency = currency;
        PayPalOrderId = payPalOrderId;
        PayPalAuthorizationId = payPalAuthorizationId;
        AuthorizedAmount = authorizedAmount;
        AuthorizationExpiresAt = authorizationExpiresAt;
        SavedPaymentMethodId = savedPaymentMethodId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public int OrderId { get; private set; }
    public string Currency { get; private set; }

    /// <summary>The PayPal order (checkout) id that carries the authorization.</summary>
    public string PayPalOrderId { get; private set; }

    /// <summary>The current/latest authorization id — replaced in place on reauthorization.</summary>
    public string PayPalAuthorizationId { get; private set; }

    public decimal AuthorizedAmount { get; private set; }
    public DateTimeOffset AuthorizationExpiresAt { get; private set; }

    public string? PayPalCaptureId { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }

    /// <summary>Set only when the order was paid with one of the shopper's saved cards.</summary>
    public int? SavedPaymentMethodId { get; private set; }

    /// <summary>Cumulative amount refunded against the capture. Starts at 0.</summary>
    public decimal TotalRefundedAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Refund> _refunds = new List<Refund>();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    /// <summary>Replaces the current authorization id/expiry after a successful reauthorization.</summary>
    public void RecordReauthorization(string newAuthorizationId, DateTimeOffset newExpiresAt)
    {
        Guard.Against.NullOrEmpty(newAuthorizationId, nameof(newAuthorizationId));
        PayPalAuthorizationId = newAuthorizationId;
        AuthorizationExpiresAt = newExpiresAt;
        Touch();
    }

    /// <summary>Records the successful capture at fulfilment, including PayPal's fee and net proceeds.</summary>
    public void RecordCapture(string captureId, decimal capturedAmount, decimal? feeAmount, decimal? netAmount)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        PayPalCaptureId = captureId;
        CapturedAmount = capturedAmount;
        PayPalFeeAmount = feeAmount;
        NetAmount = netAmount;
        Touch();
    }

    /// <summary>Marker for a voided authorization (cancel-before-fulfilment). Lifecycle state lives on Order.</summary>
    public void RecordVoid() => Touch();

    /// <summary>Appends a refund and increases the cumulative refunded amount.</summary>
    public void RecordRefund(Refund refund)
    {
        Guard.Against.Null(refund, nameof(refund));
        _refunds.Add(refund);
        TotalRefundedAmount += refund.Amount;
        Touch();
    }

    /// <summary>Amount still refundable against the capture (captured minus already refunded).</summary>
    public decimal RemainingRefundable() => (CapturedAmount ?? 0m) - TotalRefundedAmount;

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
