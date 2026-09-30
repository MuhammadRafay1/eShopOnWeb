namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

public class BillingAddressInput
{
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AdminArea2 { get; set; } // city
    public string? AdminArea1 { get; set; } // state/province
    public string? PostalCode { get; set; }
    public string CountryCode { get; set; } = string.Empty;
}
