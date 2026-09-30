using System;
using System.Collections.Generic;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

/// <summary>
/// Companion aggregate to Order: carries the PayPal-owned state (hold, capture, refunds)
/// so a later request can act on it. Order.PaymentStatus remains the authoritative state;
/// this aggregate never stores PAN/CVV, only PayPal ids, statuses and amounts.
/// </summary>
public class Payment : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    public Payment(int orderId, string buyerId, string currencyCode, decimal authorizedAmount)
    {
        Guard.Against.NegativeOrZero(orderId, nameof(orderId));
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));
        Guard.Against.NegativeOrZero(authorizedAmount, nameof(authorizedAmount));

        OrderId = orderId;
        BuyerId = buyerId;
        CurrencyCode = currencyCode;
        AuthorizedAmount = authorizedAmount;
        IdempotencyNonce = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// A nonce generated once when this Payment is created, folded into every PayPal-Request-Id
    /// this order's money-moving calls use. PayPal remembers a PayPal-Request-Id (and replays its
    /// original response) for hours after it is first seen, so a request id derived from OrderId
    /// alone would collide the moment OrderId is reused - which happens whenever this app restarts
    /// against the in-memory database. The nonce keeps request ids unique per Payment while still
    /// deterministic across retries of the *same* Payment (so genuine double-clicks still dedupe).
    /// </summary>
    public string IdempotencyNonce { get; private set; }

    public int OrderId { get; private set; }
    public string BuyerId { get; private set; }
    public string CurrencyCode { get; private set; }
    public decimal AuthorizedAmount { get; private set; }

    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }

    private readonly List<PaymentRefund> _refunds = new List<PaymentRefund>();
    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();

    public void SetAuthorization(string payPalOrderId, string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    public void RenewAuthorization(string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    public void SetCapture(string captureId, string status, decimal capturedAmount, decimal? fee, decimal? net)
    {
        CaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = capturedAmount;
        PayPalFee = fee;
        NetAmount = net;
    }

    public void AddRefund(PaymentRefund refund)
    {
        Guard.Against.Null(refund, nameof(refund));
        _refunds.Add(refund);
    }

    public PaymentRefund? FindRefundByIdempotencyKey(string idempotencyKey)
    {
        foreach (var refund in _refunds)
        {
            if (string.Equals(refund.IdempotencyKey, idempotencyKey, StringComparison.Ordinal))
            {
                return refund;
            }
        }
        return null;
    }

    public decimal TotalRefunded()
    {
        var total = 0m;
        foreach (var refund in _refunds)
        {
            if (string.Equals(refund.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(refund.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                total += refund.Amount;
            }
        }
        return total;
    }

    public decimal RefundableRemaining() => (CapturedAmount ?? 0m) - TotalRefunded();
}
