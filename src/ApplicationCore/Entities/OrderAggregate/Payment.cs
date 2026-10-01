using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// The money/PayPal state that follows an <see cref="Order"/> through authorize → capture → refund.
/// 1:1 with <see cref="Order"/>. Holds every PayPal-owned identifier and status the integration needs
/// to act on the payment later (the hold, the capture, the refunds) — not only the request that started it.
/// Raw PayPal wire statuses are kept as strings; PayPal SDK types never reach this layer.
/// </summary>
public class Payment : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    public Payment(int orderId, decimal amount, string currencyCode)
    {
        Guard.Against.NegativeOrZero(amount, nameof(amount));
        Guard.Against.NullOrEmpty(currencyCode, nameof(currencyCode));

        OrderId = orderId;
        Amount = amount;
        CurrencyCode = currencyCode;
        Status = PaymentStatus.Created;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public int OrderId { get; private set; }
    public PaymentStatus Status { get; private set; }

    /// <summary>The amount to be held/captured — the order total, snapshotted here to the cent.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Currency snapshotted per-payment so a later config change can't reinterpret historical amounts.</summary>
    public string CurrencyCode { get; private set; }

    // ---- The hold (authorization) ----
    public string? PayPalOrderId { get; private set; }
    public string? PayPalAuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    // ---- The capture ----
    public string? PayPalCaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFeeAmount { get; private set; }
    public decimal? NetAmount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    /// <summary>Record a successful authorization (the hold).</summary>
    public void SetAuthorized(string payPalOrderId, string authorizationId, string authorizationStatus, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));

        PayPalOrderId = payPalOrderId;
        PayPalAuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiresAt = expiresAt;
        Status = PaymentStatus.Authorized;
        Touch();
    }

    /// <summary>Replace the authorization details after a reauthorization (the id may change — take it from the response).</summary>
    public void UpdateAuthorization(string authorizationId, string authorizationStatus, DateTimeOffset? expiresAt)
    {
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        PayPalAuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        AuthorizationExpiresAt = expiresAt;
        Touch();
    }

    /// <summary>Record a successful capture at fulfilment, with PayPal's reported fee/net breakdown.</summary>
    public void SetCaptured(string captureId, string captureStatus, decimal capturedAmount, decimal? feeAmount, decimal? netAmount)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        PayPalCaptureId = captureId;
        CaptureStatus = captureStatus;
        CapturedAmount = capturedAmount;
        PayPalFeeAmount = feeAmount;
        NetAmount = netAmount;
        Status = PaymentStatus.Captured;
        Touch();
    }

    /// <summary>Record that the authorization was voided (cancel before fulfilment).</summary>
    public void SetVoided()
    {
        Status = PaymentStatus.Voided;
        AuthorizationStatus = "VOIDED";
        Touch();
    }

    public void SetFailed()
    {
        Status = PaymentStatus.Failed;
        Touch();
    }

    /// <summary>Attach a refund (the PENDING claim row) and recompute the aggregate refund status.</summary>
    public void AddRefund(Refund refund)
    {
        Guard.Against.Null(refund, nameof(refund));
        _refunds.Add(refund);
        RecomputeRefundStatus();
        Touch();
    }

    /// <summary>Recompute Captured / PartiallyRefunded / Refunded from the refunds that count.</summary>
    public void RecomputeRefundStatus()
    {
        if (CapturedAmount is null)
        {
            return;
        }

        var refunded = TotalRefunded();
        if (refunded <= 0m)
        {
            Status = PaymentStatus.Captured;
        }
        else if (refunded >= CapturedAmount.Value)
        {
            Status = PaymentStatus.Refunded;
        }
        else
        {
            Status = PaymentStatus.PartiallyRefunded;
        }
        Touch();
    }

    /// <summary>Sum of refunds that count toward the refunded total (excludes failed/cancelled).</summary>
    public decimal TotalRefunded() =>
        _refunds.Where(r => r.CountsTowardRefundedTotal).Sum(r => r.Amount);

    /// <summary>The amount still refundable — can never exceed what was captured.</summary>
    public decimal RefundableRemaining() =>
        (CapturedAmount ?? 0m) - TotalRefunded();

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
