using System;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class OrderRefundDto
{
    public int RefundId { get; set; }
    public string PayPalRefundId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedDate { get; set; }
}

public class OrderPaymentDto
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;

    public string? PayPalOrderId { get; set; }
    public string? AuthorizationId { get; set; }
    public decimal AuthorizedAmount { get; set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; set; }

    public string? CaptureId { get; set; }
    public decimal? CapturedGrossAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }

    public decimal TotalRefundedAmount { get; set; }
    public OrderRefundDto[] Refunds { get; set; } = Array.Empty<OrderRefundDto>();

    public static OrderPaymentDto From(OrderPayment payment) => new()
    {
        OrderId = payment.OrderId,
        Status = payment.Status.ToString(),
        CurrencyCode = payment.CurrencyCode,
        PayPalOrderId = payment.PayPalOrderId,
        AuthorizationId = payment.AuthorizationId,
        AuthorizedAmount = payment.AuthorizedAmount,
        AuthorizationExpiresAt = payment.AuthorizationExpiresAt,
        CaptureId = payment.CaptureId,
        CapturedGrossAmount = payment.CapturedGrossAmount,
        PayPalFee = payment.PayPalFee,
        NetAmount = payment.NetAmount,
        TotalRefundedAmount = payment.TotalRefundedAmount,
        Refunds = payment.Refunds.Select(r => new OrderRefundDto
        {
            RefundId = r.Id,
            PayPalRefundId = r.PayPalRefundId,
            Amount = r.Amount,
            Status = r.Status,
            CreatedDate = r.CreatedDate
        }).ToArray()
    };
}
