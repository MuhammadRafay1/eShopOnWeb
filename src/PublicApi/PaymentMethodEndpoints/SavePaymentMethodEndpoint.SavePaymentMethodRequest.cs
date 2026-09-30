namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SaveCardBillingAddressDto
{
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string AdminArea2 { get; set; } = string.Empty;
    public string AdminArea1 { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "US";
}

public class SaveCardDto
{
    public string Name { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    /// <summary>YYYY-MM</summary>
    public string Expiry { get; set; } = string.Empty;
    public string SecurityCode { get; set; } = string.Empty;
    public SaveCardBillingAddressDto BillingAddress { get; set; } = new();
}

public class SavePaymentMethodRequest : BaseRequest
{
    /// <summary>Set server-side from the caller's JWT after binding.</summary>
    public string OwnerId { get; set; } = string.Empty;

    public SaveCardDto Card { get; set; } = new();
}
