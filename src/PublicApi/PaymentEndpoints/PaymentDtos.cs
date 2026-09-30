using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;

namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

/// <summary>Card details as accepted on the wire. Never persisted; the PAN/CVV are read once to build the PayPal call.</summary>
public class CardDto
{
    public string Number { get; set; } = "";
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string SecurityCode { get; set; } = "";
    public string CardholderName { get; set; } = "";
    public BillingAddressDto BillingAddress { get; set; } = new();

    public CardDetails ToCardDetails() => new(
        Number, ExpiryMonth, ExpiryYear, SecurityCode, CardholderName,
        BillingAddress.Line1, BillingAddress.Line2, BillingAddress.City, BillingAddress.State,
        BillingAddress.PostalCode, BillingAddress.CountryCode);
}

public class BillingAddressDto
{
    public string Line1 { get; set; } = "";
    public string? Line2 { get; set; }
    public string City { get; set; } = "";
    public string? State { get; set; }
    public string PostalCode { get; set; } = "";
    public string CountryCode { get; set; } = "";
}

/// <summary>Shipping address for a new order.</summary>
public class ShippingAddressDto
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string? State { get; set; }
    public string Country { get; set; } = "";
    public string ZipCode { get; set; } = "";
}

public class OrderLineDto
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class OrderItemSummaryDto
{
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}
