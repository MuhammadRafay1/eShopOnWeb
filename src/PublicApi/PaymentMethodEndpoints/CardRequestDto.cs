using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>
/// Raw card details as submitted by a caller for a one-off payment or a saved-card creation.
/// Never persisted or logged as-is - mapped straight into a PayPal request and discarded.
/// </summary>
public class CardRequestDto
{
    public string Number { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public string SecurityCode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public BillingAddressRequestDto? BillingAddress { get; set; }
}

public class BillingAddressRequestDto
{
    public string? AddressLine1 { get; set; }
    public string? AdminArea1 { get; set; }
    public string? AdminArea2 { get; set; }
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = string.Empty;
}

public static class CardRequestMapping
{
    public static PayPalCardInput ToPayPalCardInput(this CardRequestDto card)
    {
        return new PayPalCardInput
        {
            Name = card.Name,
            Number = card.Number,
            Expiry = card.Expiry,
            SecurityCode = card.SecurityCode,
            BillingAddress = card.BillingAddress is null
                ? null
                : new PayPalBillingAddressInput
                {
                    AddressLine1 = card.BillingAddress.AddressLine1,
                    AdminArea1 = card.BillingAddress.AdminArea1,
                    AdminArea2 = card.BillingAddress.AdminArea2,
                    PostalCode = card.BillingAddress.PostalCode,
                    CountryCode = card.BillingAddress.CountryCode
                }
        };
    }
}
