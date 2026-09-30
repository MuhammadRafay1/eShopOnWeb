using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class OrderDto
{
    public int OrderId { get; set; }
    public string Status { get; set; } = default!;
    public decimal Total { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();

    public string? Currency { get; set; }
    public string? PayPalOrderId { get; set; }
    public string? AuthorizationId { get; set; }
    public string? AuthorizationStatus { get; set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; set; }
    public string? CaptureId { get; set; }
    public string? CaptureStatus { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal TotalRefunded { get; set; }
    public decimal RefundableRemaining { get; set; }
    public List<RefundDto> Refunds { get; set; } = new();

    public static OrderDto FromDomain(Order order)
    {
        var dto = new OrderDto
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            Total = order.Total(),
            OrderDate = order.OrderDate,
            Items = order.OrderItems.Select(i => new OrderItemDto
            {
                CatalogItemId = i.ItemOrdered.CatalogItemId,
                ProductName = i.ItemOrdered.ProductName,
                UnitPrice = i.UnitPrice,
                Units = i.Units
            }).ToList(),
            TotalRefunded = order.TotalRefunded(),
            RefundableRemaining = order.RefundableRemaining()
        };

        if (order.Payment is { } payment)
        {
            dto.Currency = payment.Currency;
            dto.PayPalOrderId = payment.PayPalOrderId;
            dto.AuthorizationId = payment.AuthorizationId;
            dto.AuthorizationStatus = payment.AuthorizationStatus;
            dto.AuthorizationExpiresAt = payment.AuthorizationExpiresAt;
            dto.CaptureId = payment.CaptureId;
            dto.CaptureStatus = payment.CaptureStatus;
            dto.CapturedAmount = payment.CapturedAmount;
            dto.PayPalFee = payment.PayPalFee;
            dto.NetAmount = payment.NetAmount;
            dto.Refunds = payment.Refunds.Select(r => new RefundDto
            {
                RefundId = r.PayPalRefundId,
                Amount = r.Amount,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        return dto;
    }
}

public class OrderItemDto
{
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}

public class RefundDto
{
    public string RefundId { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
