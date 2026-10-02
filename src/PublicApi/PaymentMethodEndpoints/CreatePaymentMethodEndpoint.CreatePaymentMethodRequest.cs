namespace Microsoft.eShopWeb.PublicApi.PaymentMethodEndpoints;

public class SaveCardRequestBody
{
    public string Number { get; set; } = string.Empty;

    /// <summary>ISO-8601 year-month, e.g. "2030-01".</summary>
    public string ExpiryYearMonth { get; set; } = string.Empty;
    public string SecurityCode { get; set; } = string.Empty;
    public string? CardholderName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}

public class CreatePaymentMethodRequest : BaseRequest
{
    public string BuyerId { get; }
    public SaveCardRequestBody Card { get; }

    public CreatePaymentMethodRequest(string buyerId, SaveCardRequestBody card)
    {
        BuyerId = buyerId;
        Card = card;
    }
}
