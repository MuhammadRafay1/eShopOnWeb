namespace Microsoft.eShopWeb.PublicApi;

/// <summary>
/// One-off card details supplied by a caller. Never persisted or logged - only ever forwarded to PayPal.
/// </summary>
public class CardDetailsDto
{
    public string Number { get; set; } = string.Empty;

    /// <summary>"YYYY-MM"</summary>
    public string Expiry { get; set; } = string.Empty;

    public string SecurityCode { get; set; } = string.Empty;
    public string CardholderName { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Country { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}
