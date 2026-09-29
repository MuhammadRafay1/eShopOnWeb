using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

// ---- Requests ----

public class OrderItemRequest
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class CardRequest
{
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public string Expiry { get; set; } = "";          // "YYYY-MM"
    public string SecurityCode { get; set; } = "";
    public BillingAddressRequest? BillingAddress { get; set; }
}

public class BillingAddressRequest
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AdminArea2 { get; set; }   // city / town
    public string? AdminArea1 { get; set; }   // state / province
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }  // 2-char ISO 3166-1
}

public class ShippingAddressRequest
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "";
    public string ZipCode { get; set; } = "";
}

public class CreateOrderRequest
{
    public List<OrderItemRequest> Items { get; set; } = new();
    public ShippingAddressRequest? ShippingAddress { get; set; }
}

public class PayOrderRequest
{
    // Populated from route + token in the endpoint (never trusted from the body).
    public int OrderId { get; set; }
    public string BuyerId { get; set; } = "";

    public CardRequest? Card { get; set; }
    public int? PaymentMethodId { get; set; }
}

public class RefundOrderRequest
{
    public int OrderId { get; set; }
    public decimal? Amount { get; set; }
    public string IdempotencyKey { get; set; } = "";
}

// ---- Responses ----

public class RefundDto
{
    public int RefundId { get; set; }
    public string PayPalRefundId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class PaymentDto
{
    public string Status { get; set; } = "";
    public string PayPalOrderId { get; set; } = "";
    public string PayPalAuthorizationId { get; set; } = "";
    public string AuthorizationStatus { get; set; } = "";
    public DateTimeOffset AuthorizationExpiresAt { get; set; }
    public int ReauthorizationCount { get; set; }
    public string? PayPalCaptureId { get; set; }
    public string? CaptureStatus { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFee { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public string? LastOperatorError { get; set; }
    public List<RefundDto> Refunds { get; set; } = new();
}

public class OrderItemDto
{
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}

public class OrderResponse
{
    public int OrderId { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public string Currency { get; set; } = "";
    public DateTimeOffset OrderDate { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public PaymentDto? Payment { get; set; }
}

public class MyOrdersResponse
{
    public List<OrderResponse> Orders { get; set; } = new();
}

public class RefundResponse
{
    public int RefundId { get; set; }
    public string PayPalRefundId { get; set; } = "";
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public decimal TotalRefunded { get; set; }
    public string OrderStatus { get; set; } = "";
}

/// <summary>Maps domain results to API response DTOs. The currency label comes from configuration.</summary>
public static class OrderResponseMapper
{
    public static OrderResponse ToResponse(OrderPaymentResult result, string currency)
    {
        var order = result.Order;
        var payment = result.Payment;
        return new OrderResponse
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            Total = order.Total(),
            Currency = payment?.Currency ?? currency,
            OrderDate = order.OrderDate,
            Items = order.OrderItems.Select(i => new OrderItemDto
            {
                CatalogItemId = i.ItemOrdered.CatalogItemId,
                ProductName = i.ItemOrdered.ProductName,
                UnitPrice = i.UnitPrice,
                Units = i.Units,
            }).ToList(),
            Payment = payment is null ? null : new PaymentDto
            {
                Status = payment.Status.ToString(),
                PayPalOrderId = payment.PayPalOrderId,
                PayPalAuthorizationId = payment.PayPalAuthorizationId,
                AuthorizationStatus = payment.AuthorizationStatus,
                AuthorizationExpiresAt = payment.AuthorizationExpiresAt,
                ReauthorizationCount = payment.ReauthorizationCount,
                PayPalCaptureId = payment.PayPalCaptureId,
                CaptureStatus = payment.CaptureStatus,
                CapturedAmount = payment.CapturedAmount,
                PayPalFee = payment.PayPalFeeAmount,
                NetAmount = payment.NetAmount,
                RefundedAmount = payment.RefundedAmount,
                LastOperatorError = payment.LastOperatorError,
                Refunds = payment.Refunds.Select(r => new RefundDto
                {
                    RefundId = r.Id,
                    PayPalRefundId = r.PayPalRefundId,
                    Amount = r.Amount,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                }).ToList(),
            },
        };
    }

    public static CardDetails ToCardDetails(CardRequest card)
    {
        CardBillingAddress? billing = card.BillingAddress is null
            ? null
            : new CardBillingAddress(
                card.BillingAddress.AddressLine1,
                card.BillingAddress.AddressLine2,
                card.BillingAddress.AdminArea2,
                card.BillingAddress.AdminArea1,
                card.BillingAddress.PostalCode,
                card.BillingAddress.CountryCode);

        return new CardDetails(card.Name, card.Number, card.Expiry, card.SecurityCode, billing);
    }
}
