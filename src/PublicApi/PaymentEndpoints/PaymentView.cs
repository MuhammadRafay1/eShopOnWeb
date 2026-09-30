using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>
/// The payment state as exposed to callers — mirrors the ids/status PayPal owns for the hold, the
/// capture and the refunds, so a later request can act on it. No card details.
/// </summary>
public class PaymentView
{
    public int PaymentId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public string? AuthorizationId { get; set; }
    public string AuthorizationStatus { get; set; } = string.Empty;
    public DateTimeOffset? AuthorizationExpiresAt { get; set; }

    public string? CaptureId { get; set; }
    public string? CaptureStatus { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }

    public decimal RefundedAmount { get; set; }
    public decimal RemainingRefundable { get; set; }
    public int? SavedPaymentMethodId { get; set; }

    public static PaymentView From(Payment payment) => new()
    {
        PaymentId = payment.Id,
        Currency = payment.Currency,
        Amount = payment.Amount,
        AuthorizationId = payment.PayPalAuthorizationId,
        AuthorizationStatus = payment.AuthorizationStatus,
        AuthorizationExpiresAt = payment.AuthorizationExpiresAt,
        CaptureId = payment.PayPalCaptureId,
        CaptureStatus = payment.CaptureStatus,
        CapturedAmount = payment.CapturedAmount,
        PayPalFee = payment.PayPalFeeAmount,
        NetAmount = payment.NetAmount,
        RefundedAmount = payment.RefundedAmount,
        RemainingRefundable = payment.RemainingRefundable,
        SavedPaymentMethodId = payment.PaymentMethodId
    };
}
