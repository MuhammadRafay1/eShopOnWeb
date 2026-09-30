using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class PaymentDto
{
    public string PayPalOrderId { get; set; } = "";
    public string AuthorizationId { get; set; } = "";
    public string AuthorizationStatus { get; set; } = "";
    public string? CaptureId { get; set; }
    public string? CaptureStatus { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public string Currency { get; set; } = "";
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public decimal TotalRefunded { get; set; }
    public List<RefundDto> Refunds { get; set; } = new();

    public static PaymentDto? FromEntity(Payment? payment)
    {
        if (payment is null) return null;
        return new PaymentDto
        {
            PayPalOrderId = payment.PayPalOrderId,
            AuthorizationId = payment.AuthorizationId,
            AuthorizationStatus = payment.AuthorizationStatus,
            CaptureId = payment.CaptureId,
            CaptureStatus = payment.CaptureStatus,
            CapturedAmount = payment.CapturedAmount,
            PayPalFee = payment.PayPalFee,
            NetAmount = payment.NetAmount,
            Currency = payment.Currency,
            CardBrand = payment.CardBrand,
            CardLast4 = payment.CardLast4,
            TotalRefunded = payment.TotalRefunded(),
            Refunds = payment.Refunds.Select(RefundDto.FromEntity).ToList()
        };
    }
}

public class RefundDto
{
    public int RefundId { get; set; }
    public string PayPalRefundId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";

    public static RefundDto FromEntity(PaymentRefund refund) => new()
    {
        RefundId = refund.Id,
        PayPalRefundId = refund.RefundId,
        Amount = refund.Amount,
        Status = refund.Status
    };
}
