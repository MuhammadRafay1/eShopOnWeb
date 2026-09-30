using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// The PayPal-owned state of an order's payment: the authorization (hold), the capture (money taken),
/// and any refunds. Owned by <see cref="Order"/>. Enough of PayPal's own ids/statuses are kept here
/// that a later request (fulfil, cancel, refund) can act on the same authorization/capture instead of
/// starting a new one.
/// </summary>
public class Payment
{
    private readonly List<Refund> _refunds = new();

#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }
#pragma warning restore CS8618

    public Payment(string currency)
    {
        Currency = currency;
    }

    public string Currency { get; private set; }

    public string? PayPalOrderId { get; private set; }
    public string? AuthorizationId { get; private set; }
    public string? AuthorizationStatus { get; private set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; private set; }

    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }

    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    internal void MarkAuthorized(string payPalOrderId, string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    internal void ReplaceAuthorization(string authorizationId, string status, DateTimeOffset? expiresAt)
    {
        AuthorizationId = authorizationId;
        AuthorizationStatus = status;
        AuthorizationExpiresAt = expiresAt;
    }

    internal void MarkVoided()
    {
        AuthorizationStatus = "VOIDED";
    }

    internal void MarkCaptured(string captureId, string status, decimal capturedAmount, decimal payPalFee, decimal netAmount)
    {
        CaptureId = captureId;
        CaptureStatus = status;
        CapturedAmount = capturedAmount;
        PayPalFee = payPalFee;
        NetAmount = netAmount;
        CapturedAt = DateTimeOffset.UtcNow;
    }

    internal void AddRefund(Refund refund)
    {
        _refunds.Add(refund);
    }

    public decimal TotalRefunded() => _refunds.Sum(r => r.Amount);

    public decimal RefundableRemaining() => (CapturedAmount ?? 0m) - TotalRefunded();

    public Refund? FindRefundByKey(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
}
