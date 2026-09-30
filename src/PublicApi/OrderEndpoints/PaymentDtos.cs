using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class OrderItemDto
{
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}

public class RefundDto
{
    public int RefundId { get; set; }
    public string PayPalRefundId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>The payment state PayPal owns for an order, as returned to a caller (never a vault id or PAN).</summary>
public class PaymentDto
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";

    public string? AuthorizationId { get; set; }
    public string? AuthorizationStatus { get; set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; set; }

    public string? CaptureId { get; set; }
    public string? CaptureStatus { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }

    public decimal TotalRefunded { get; set; }
    public decimal RemainingRefundable { get; set; }
    public List<RefundDto> Refunds { get; set; } = new();

    public static PaymentDto From(Payment p) => new()
    {
        Amount = p.Amount,
        Currency = p.Currency,
        AuthorizationId = p.AuthorizationId,
        AuthorizationStatus = p.AuthorizationStatus,
        AuthorizationExpiresAt = p.AuthorizationExpiresAt,
        CaptureId = p.CaptureId,
        CaptureStatus = p.CaptureStatus,
        CapturedAmount = p.CapturedAmount,
        PayPalFee = p.PayPalFee,
        NetAmount = p.NetAmount,
        TotalRefunded = p.TotalRefunded,
        RemainingRefundable = p.RemainingRefundable,
        Refunds = p.Refunds
            .OrderBy(r => r.CreatedAt)
            .Select(r => new RefundDto
            {
                RefundId = r.Id,
                PayPalRefundId = r.PayPalRefundId,
                Amount = r.Amount,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            }).ToList()
    };

    public static List<OrderItemDto> ItemsOf(Order order) =>
        order.OrderItems.Select(i => new OrderItemDto
        {
            CatalogItemId = i.ItemOrdered.CatalogItemId,
            ProductName = i.ItemOrdered.ProductName,
            UnitPrice = i.UnitPrice,
            Units = i.Units
        }).ToList();
}
