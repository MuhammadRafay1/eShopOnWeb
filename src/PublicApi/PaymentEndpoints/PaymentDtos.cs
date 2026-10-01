using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>An order with its payment state, as returned by the order endpoints.</summary>
public class OrderPaymentDto
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public DateTimeOffset? FulfilledAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public PaymentDto? Payment { get; set; }
    public List<OrderLineDto> Items { get; set; } = new();
}

public class OrderLineDto
{
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}

public class PaymentDto
{
    public string Status { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? PayPalOrderId { get; set; }
    public string? AuthorizationId { get; set; }
    public string? AuthorizationStatus { get; set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; set; }
    public string? CaptureId { get; set; }
    public string? CaptureStatus { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFeeAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal RefundableRemaining { get; set; }
    public List<RefundDto> Refunds { get; set; } = new();
}

public class RefundDto
{
    public int Id { get; set; }
    public string? PayPalRefundId { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class PaymentMethodDto
{
    public int PaymentMethodId { get; set; }
    public string? Alias { get; set; }
    public string? Last4 { get; set; }
    public string? Brand { get; set; }
    public string? Expiry { get; set; }
}

/// <summary>Maps domain entities to the response DTOs. Only safe fields — never full card data — are exposed.</summary>
public static class PaymentMappings
{
    public static OrderPaymentDto ToDto(this Order order) => new()
    {
        OrderId = order.Id,
        Status = order.Status.ToString(),
        Total = order.Total(),
        OrderDate = order.OrderDate,
        FulfilledAt = order.FulfilledAt,
        CancelledAt = order.CancelledAt,
        Items = order.OrderItems.Select(i => new OrderLineDto
        {
            CatalogItemId = i.ItemOrdered.CatalogItemId,
            ProductName = i.ItemOrdered.ProductName,
            UnitPrice = i.UnitPrice,
            Units = i.Units
        }).ToList(),
        Payment = order.Payment is null ? null : order.Payment.ToDto()
    };

    public static PaymentDto ToDto(this Payment payment) => new()
    {
        Status = payment.Status.ToString(),
        CurrencyCode = payment.CurrencyCode,
        Amount = payment.Amount,
        PayPalOrderId = payment.PayPalOrderId,
        AuthorizationId = payment.PayPalAuthorizationId,
        AuthorizationStatus = payment.AuthorizationStatus,
        AuthorizationExpiresAt = payment.AuthorizationExpiresAt,
        CaptureId = payment.PayPalCaptureId,
        CaptureStatus = payment.CaptureStatus,
        CapturedAmount = payment.CapturedAmount,
        PayPalFeeAmount = payment.PayPalFeeAmount,
        NetAmount = payment.NetAmount,
        RefundedAmount = payment.TotalRefunded(),
        RefundableRemaining = payment.RefundableRemaining(),
        Refunds = payment.Refunds.Select(r => new RefundDto
        {
            Id = r.Id,
            PayPalRefundId = r.PayPalRefundId,
            Status = r.Status,
            Amount = r.Amount,
            CreatedAt = r.CreatedAt
        }).ToList()
    };

    public static PaymentMethodDto ToDto(this PaymentMethod method) => new()
    {
        PaymentMethodId = method.Id,
        Alias = method.Alias,
        Last4 = method.Last4,
        Brand = method.Brand,
        Expiry = method.Expiry
    };
}
