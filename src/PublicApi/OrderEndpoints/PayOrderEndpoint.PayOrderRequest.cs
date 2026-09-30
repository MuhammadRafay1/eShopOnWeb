namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class PayBillingAddressDto
{
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string AdminArea2 { get; set; } = string.Empty;
    public string AdminArea1 { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "US";
}

public class PayCardDto
{
    public string Name { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    /// <summary>YYYY-MM</summary>
    public string Expiry { get; set; } = string.Empty;
    public string SecurityCode { get; set; } = string.Empty;
    public PayBillingAddressDto BillingAddress { get; set; } = new();
}

public class PayOrderRequest : BaseRequest
{
    /// <summary>Bound from the route.</summary>
    public int OrderId { get; set; }

    /// <summary>Set server-side from the caller's JWT after binding.</summary>
    public string BuyerId { get; set; } = string.Empty;

    /// <summary>Exactly one of Card / SavedPaymentMethodId must be supplied.</summary>
    public PayCardDto? Card { get; set; }
    public int? SavedPaymentMethodId { get; set; }
}
