namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SavePaymentMethodRequest : BaseRequest
{
    // Transient - only ever flows through to PayPal's vault, never persisted by this app.
    public string CardNumber { get; set; } = "";
    public string ExpiryYearMonth { get; set; } = ""; // "YYYY-MM"
    public string CardholderName { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string CountryCode { get; set; } = "US";
}
