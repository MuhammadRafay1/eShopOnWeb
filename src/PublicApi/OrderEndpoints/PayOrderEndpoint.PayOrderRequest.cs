using System.Text.Json.Serialization;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class PayOrderRequest : BaseRequest
{
    [JsonIgnore]
    public int OrderId { get; set; }

    /// <summary>Pay with a previously saved card instead of one-off card details.</summary>
    public int? PaymentMethodId { get; set; }

    // One-off card details (ignored when PaymentMethodId is supplied). Never persisted -
    // this DTO only ever flows through to PayPal.
    public string? CardNumber { get; set; }
    public string? ExpiryYearMonth { get; set; } // "YYYY-MM"
    public string? CardholderName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
}
