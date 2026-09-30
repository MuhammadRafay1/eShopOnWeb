using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>A full view of an order and its payment state, safe to return to a caller.</summary>
public class OrderSummaryDto
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset OrderDate { get; set; }
    public decimal Total { get; set; }
    public PaymentSummaryDto? Payment { get; set; }

    public static OrderSummaryDto From(Order order) => new OrderSummaryDto
    {
        OrderId = order.Id,
        Status = order.Status.ToString(),
        OrderDate = order.OrderDate,
        Total = order.Total(),
        Payment = order.Payment is null ? null : PaymentSummaryDto.From(order.Payment)
    };
}

/// <summary>Payment state derived from what PayPal reported. Never carries card details.</summary>
public class PaymentSummaryDto
{
    public string Currency { get; set; } = string.Empty;
    public string PayPalOrderId { get; set; } = string.Empty;

    public string AuthorizationId { get; set; } = string.Empty;
    public string AuthorizationStatus { get; set; } = string.Empty;
    public decimal AuthorizedAmount { get; set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; set; }

    public string? CaptureId { get; set; }
    public string? CaptureStatus { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }

    public decimal RefundedAmount { get; set; }
    public string? PaymentMethodId { get; set; }
    public List<RefundDto> Refunds { get; set; } = new();

    public static PaymentSummaryDto From(Payment payment) => new PaymentSummaryDto
    {
        Currency = payment.Currency,
        PayPalOrderId = payment.PayPalOrderId,
        AuthorizationId = payment.AuthorizationId,
        AuthorizationStatus = payment.AuthorizationStatus,
        AuthorizedAmount = payment.AuthorizedAmount,
        AuthorizationExpiresAt = payment.AuthorizationExpiresAt,
        CaptureId = payment.CaptureId,
        CaptureStatus = payment.CaptureStatus,
        CapturedAmount = payment.CapturedAmount,
        PayPalFee = payment.PayPalFee,
        NetAmount = payment.NetAmount,
        CapturedAt = payment.CapturedAt,
        RefundedAmount = payment.RefundedAmount,
        PaymentMethodId = payment.PaymentMethodId,
        Refunds = payment.Refunds
            .Select(r => new RefundDto
            {
                RefundId = r.PayPalRefundId,
                Amount = r.Amount,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            }).ToList()
    };
}

public class RefundDto
{
    public string RefundId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
