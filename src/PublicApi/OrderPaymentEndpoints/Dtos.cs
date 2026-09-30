using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.PublicApi.OrderPaymentEndpoints;

public class OrderLineDto
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

public class ShipToAddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public class CardDto
{
    public string Number { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public string SecurityCode { get; set; } = string.Empty;
    public string? CardholderName { get; set; }
    public CardBillingAddressDto BillingAddress { get; set; } = new();

    // Never let a redacted-but-still-logged accident leak the PAN.
    public override string ToString() => "[CardDto redacted]";

    public CardInput ToCardInput() => new()
    {
        Number = Number,
        Expiry = $"{ExpiryYear:D4}-{ExpiryMonth:D2}",
        SecurityCode = SecurityCode,
        CardholderName = CardholderName,
        BillingAddress = new CardBillingAddress
        {
            Line1 = BillingAddress.Line1,
            Line2 = BillingAddress.Line2,
            City = BillingAddress.City,
            State = BillingAddress.State,
            PostalCode = BillingAddress.PostalCode,
            CountryCode = BillingAddress.CountryCode
        }
    };
}

public class CardBillingAddressDto
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

public class OrderItemSummaryDto
{
    public int CatalogItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}

public class RefundSummaryDto
{
    public int RefundId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}
