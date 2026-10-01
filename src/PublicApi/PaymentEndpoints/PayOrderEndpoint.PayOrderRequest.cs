namespace Microsoft.eShopWeb.PublicApi.PaymentEndpoints;

public class PayOrderCardRequest
{
    public string Number { get; set; } = string.Empty;

    /// <summary>ISO-8601 YYYY-MM.</summary>
    public string Expiry { get; set; } = string.Empty;
    public string? SecurityCode { get; set; }
    public string? CardholderName { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AdminArea1 { get; set; }
    public string? AdminArea2 { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>Two-letter ISO-3166-1 country code.</summary>
    public string? CountryCode { get; set; }
}

/// <summary>Exactly one of <see cref="Card"/> or <see cref="PaymentMethodId"/> must be supplied.</summary>
public class PayOrderRequest : BaseRequest
{
    public int OrderId { get; set; }
    public PayOrderCardRequest? Card { get; set; }
    public int? PaymentMethodId { get; set; }
}
