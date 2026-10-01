namespace Microsoft.eShopWeb.ApplicationCore.Payments;

/// <summary>
/// A request to authorize (hold) an order total. Carries either inline <see cref="Card"/> details for a one-off
/// payment, or a <see cref="VaultTokenId"/> naming one of the shopper's saved cards. <see cref="OrderReference"/>
/// is a stable string (the order id) the gateway derives its deterministic PayPal-Request-Id from.
/// </summary>
public sealed record PaymentAuthorizationRequest(
    string OrderReference,
    Money Amount,
    CardDetails? Card,
    string? VaultTokenId);
