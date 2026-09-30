using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

/// <summary>
/// Holds the PayPal-owned state (ids and current status for the hold, the capture, and any
/// refunds) for a single Order's payment. Child of the Order aggregate - Order remains the
/// consistency boundary. Never holds card numbers/CVV, only safe descriptors and PayPal ids.
/// </summary>
public class Payment : BaseEntity
{
#pragma warning disable CS8618 // Required by Entity Framework
    private Payment() { }

    public Payment(string payPalOrderId, string authorizationId, string authorizationStatus,
        string currency, string? cardBrand, string? cardLast4)
    {
        Guard.Against.NullOrEmpty(payPalOrderId, nameof(payPalOrderId));
        Guard.Against.NullOrEmpty(authorizationId, nameof(authorizationId));
        Guard.Against.NullOrEmpty(authorizationStatus, nameof(authorizationStatus));
        Guard.Against.NullOrEmpty(currency, nameof(currency));

        PayPalOrderId = payPalOrderId;
        AuthorizationId = authorizationId;
        AuthorizationStatus = authorizationStatus;
        Currency = currency;
        CardBrand = cardBrand;
        CardLast4 = cardLast4;
    }

    public string PayPalOrderId { get; private set; }
    public string AuthorizationId { get; private set; }
    public string AuthorizationStatus { get; private set; }
    public string? CaptureId { get; private set; }
    public string? CaptureStatus { get; private set; }
    public decimal? CapturedAmount { get; private set; }
    public decimal? PayPalFee { get; private set; }
    public decimal? NetAmount { get; private set; }
    public DateTimeOffset? CapturedAt { get; private set; }
    public string Currency { get; private set; }
    public string? CardBrand { get; private set; }
    public string? CardLast4 { get; private set; }

    private readonly List<PaymentRefund> _refunds = new();
    public IReadOnlyCollection<PaymentRefund> Refunds => _refunds.AsReadOnly();

    public void RenewAuthorization(string newAuthorizationId, string status)
    {
        Guard.Against.NullOrEmpty(newAuthorizationId, nameof(newAuthorizationId));
        Guard.Against.NullOrEmpty(status, nameof(status));

        AuthorizationId = newAuthorizationId;
        AuthorizationStatus = status;
    }

    public void RecordCapture(string captureId, string captureStatus, decimal capturedAmount,
        decimal payPalFee, decimal netAmount, DateTimeOffset capturedAt)
    {
        Guard.Against.NullOrEmpty(captureId, nameof(captureId));
        Guard.Against.NullOrEmpty(captureStatus, nameof(captureStatus));

        CaptureId = captureId;
        CaptureStatus = captureStatus;
        CapturedAmount = capturedAmount;
        PayPalFee = payPalFee;
        NetAmount = netAmount;
        CapturedAt = capturedAt;
    }

    public decimal TotalRefunded() => _refunds.Sum(r => r.Amount);

    public PaymentRefund? FindRefundByIdempotencyKey(string idempotencyKey) =>
        _refunds.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);

    public void AddRefund(PaymentRefund refund)
    {
        Guard.Against.Null(refund, nameof(refund));
        if (CapturedAmount is null)
        {
            throw new InvalidOperationException("Cannot refund a payment that has not been captured.");
        }
        if (TotalRefunded() + refund.Amount > CapturedAmount.Value)
        {
            throw new InvalidOperationException("Refund amount exceeds the amount remaining on the capture.");
        }
        _refunds.Add(refund);
    }
}
