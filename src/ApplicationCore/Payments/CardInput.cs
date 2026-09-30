namespace Microsoft.eShopWeb.ApplicationCore.Payments;

// Transient input only — never persisted, never logged, never serialized into a response.
public class CardInput
{
    public required string Number { get; init; }
    public required string Expiry { get; init; } // "YYYY-MM"
    public required string SecurityCode { get; init; }
    public string? CardholderName { get; init; }
    public required CardBillingAddress BillingAddress { get; init; }

    public override string ToString() => "[CardInput redacted]";
}

public class CardBillingAddress
{
    public required string Line1 { get; init; }
    public string? Line2 { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string PostalCode { get; init; }
    public required string CountryCode { get; init; }
}
