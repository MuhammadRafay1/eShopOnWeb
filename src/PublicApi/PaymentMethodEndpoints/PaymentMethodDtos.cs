using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.PublicApi.OrderEndpoints;

namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

/// <summary>Card details to vault. The number is used only for the outbound PayPal call.</summary>
public class CreatePaymentMethodRequest
{
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public string Expiry { get; set; } = "";          // "YYYY-MM"
    public string SecurityCode { get; set; } = "";
    public BillingAddressRequest? BillingAddress { get; set; }
}

public class CreatePaymentMethodResponse
{
    public int PaymentMethodId { get; set; }
    public string Brand { get; set; } = "";
    public string Last4 { get; set; } = "";
    public string Expiry { get; set; } = "";
}

public class PaymentMethodDto
{
    public int PaymentMethodId { get; set; }
    public string Brand { get; set; } = "";
    public string Last4 { get; set; } = "";
    public string Expiry { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class ListPaymentMethodsResponse
{
    public System.Collections.Generic.List<PaymentMethodDto> PaymentMethods { get; set; } = new();
}

public static class PaymentMethodMapper
{
    public static CardDetails ToCardDetails(CreatePaymentMethodRequest request)
    {
        CardBillingAddress? billing = request.BillingAddress is null
            ? null
            : new CardBillingAddress(
                request.BillingAddress.AddressLine1,
                request.BillingAddress.AddressLine2,
                request.BillingAddress.AdminArea2,
                request.BillingAddress.AdminArea1,
                request.BillingAddress.PostalCode,
                request.BillingAddress.CountryCode);

        return new CardDetails(request.Name, request.Number, request.Expiry, request.SecurityCode, billing);
    }

    public static PaymentMethodDto ToDto(PaymentMethod pm) => new()
    {
        PaymentMethodId = pm.Id,
        Brand = pm.CardBrand,
        Last4 = pm.Last4Digits,
        Expiry = pm.Expiry,
        CreatedAt = pm.CreatedAt,
    };
}
