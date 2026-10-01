using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.PublicApi.PaymentModels;

internal static class PaymentMapping
{
    public static CardDetails ToCardDetails(this CardDto dto) => new()
    {
        Number = dto.Number,
        Expiry = dto.Expiry,
        SecurityCode = dto.SecurityCode,
        Name = dto.Name,
        BillingAddress = new CardBillingAddress
        {
            Line1 = dto.BillingAddress.Line1,
            Line2 = dto.BillingAddress.Line2,
            City = dto.BillingAddress.City,
            State = dto.BillingAddress.State,
            PostalCode = dto.BillingAddress.PostalCode,
            CountryCode = dto.BillingAddress.CountryCode
        }
    };

    public static ShipTo ToShipTo(this AddressDto dto) =>
        new(dto.Street, dto.City, dto.State, dto.Country, dto.ZipCode);
}
