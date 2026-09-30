namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;

/// <summary>
/// Transient card details for a one-off payment or a vault request. This type lives only for the duration
/// of a single request: <see cref="Number"/> and <see cref="SecurityCode"/> are read exactly once to build
/// the PayPal call and must never be persisted or written to a log.
/// </summary>
public record CardDetails(
    string Number,
    int ExpiryMonth,
    int ExpiryYear,
    string SecurityCode,
    string CardholderName,
    string BillingAddressLine1,
    string? BillingAddressLine2,
    string City,
    string? State,
    string PostalCode,
    string CountryCode);
