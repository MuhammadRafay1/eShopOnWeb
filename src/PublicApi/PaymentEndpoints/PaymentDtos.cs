using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>A card's details on the wire. Full details are never stored or logged.</summary>
public class CardDto
{
    public string Number { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;         // YYYY-MM
    public string SecurityCode { get; set; } = string.Empty;
    public string? CardholderName { get; set; }
    public BillingAddressDto? BillingAddress { get; set; }

    public CardInput ToInput() => new(Number, Expiry, SecurityCode, CardholderName,
        BillingAddress is null
            ? null
            : new BillingAddressInput(BillingAddress.AddressLine1, BillingAddress.AddressLine2,
                BillingAddress.City, BillingAddress.State, BillingAddress.PostalCode, BillingAddress.CountryCode));
}

public class BillingAddressDto
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}

public class OrderLineDto
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class ShippingAddressDto
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? ZipCode { get; set; }
}

/// <summary>POST /api/orders — place an order from catalog items and quantities.</summary>
public class PlaceOrderRequest
{
    public List<OrderLineDto> Items { get; set; } = new();
    public ShippingAddressDto? ShipToAddress { get; set; }
}

/// <summary>POST /api/orders/{orderId}/pay — pay with a one-off card or a saved card.</summary>
public class PayOrderRequest
{
    public CardDto? Card { get; set; }
    public int? SavedPaymentMethodId { get; set; }
}

/// <summary>POST /api/orders/{orderId}/refunds — full or partial refund with a caller idempotency key.</summary>
public class RefundOrderRequest
{
    public decimal? Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

/// <summary>POST /api/payment-methods — save a card.</summary>
public class SavePaymentMethodRequest
{
    public CardDto Card { get; set; } = new();
}
