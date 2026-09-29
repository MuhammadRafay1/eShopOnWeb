using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>Raw card input shared by the pay and save-card endpoints. Never stored or logged.</summary>
public class CardDto
{
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";

    /// <summary>Expiry in YYYY-MM form (PayPal date_year_month).</summary>
    public string Expiry { get; set; } = "";
    public string? SecurityCode { get; set; }
    public BillingAddressDto? BillingAddress { get; set; }

    public CardDetails ToCardDetails() => new()
    {
        Name = Name,
        Number = Number,
        Expiry = Expiry,
        SecurityCode = SecurityCode,
        BillingAddress = BillingAddress?.ToCardBillingAddress()
    };
}

public class BillingAddressDto
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }

    /// <summary>State / province.</summary>
    public string? AdminArea1 { get; set; }

    /// <summary>City / town.</summary>
    public string? AdminArea2 { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>Two-letter ISO country code (required by PayPal).</summary>
    public string CountryCode { get; set; } = "";

    public CardBillingAddress ToCardBillingAddress() => new()
    {
        AddressLine1 = AddressLine1,
        AddressLine2 = AddressLine2,
        AdminArea1 = AdminArea1,
        AdminArea2 = AdminArea2,
        PostalCode = PostalCode,
        CountryCode = CountryCode
    };
}

public class AddressDto
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "";
    public string ZipCode { get; set; } = "";

    public Address ToAddress() => new(Street, City, State, Country, ZipCode);
}

public class OrderItemDto
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>The PayPal-owned payment state exposed on order responses.</summary>
public class PaymentDto
{
    public string PayPalOrderId { get; set; } = "";
    public string? AuthorizationId { get; set; }
    public string? AuthorizationStatus { get; set; }
    public DateTimeOffset? AuthorizationExpiresAt { get; set; }
    public string? CaptureId { get; set; }
    public string? CaptureStatus { get; set; }
    public string CurrencyCode { get; set; } = "";
    public decimal AuthorizedAmount { get; set; }
    public decimal? CapturedAmount { get; set; }
    public decimal? PayPalFeeAmount { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public List<RefundDto> Refunds { get; set; } = new();

    public static PaymentDto? From(OrderPayment? payment)
    {
        if (payment is null) return null;
        var dto = new PaymentDto
        {
            PayPalOrderId = payment.PayPalOrderId,
            AuthorizationId = payment.PayPalAuthorizationId,
            AuthorizationStatus = payment.AuthorizationStatus,
            AuthorizationExpiresAt = payment.AuthorizationExpiresAt,
            CaptureId = payment.PayPalCaptureId,
            CaptureStatus = payment.CaptureStatus,
            CurrencyCode = payment.CurrencyCode,
            AuthorizedAmount = payment.AuthorizedAmount,
            CapturedAmount = payment.CapturedAmount,
            PayPalFeeAmount = payment.PayPalFeeAmount,
            NetAmount = payment.NetAmount,
            RefundedAmount = payment.RefundedAmount
        };
        foreach (var r in payment.Refunds)
        {
            dto.Refunds.Add(new RefundDto
            {
                RefundId = r.PayPalRefundId,
                Amount = r.Amount,
                CurrencyCode = r.CurrencyCode,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            });
        }
        return dto;
    }
}

public class RefundDto
{
    public string RefundId { get; set; } = "";
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class OrderSummaryDto
{
    public int OrderId { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public PaymentDto? Payment { get; set; }
}
