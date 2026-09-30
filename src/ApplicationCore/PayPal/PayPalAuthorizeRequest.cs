namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

/// <summary>
/// Instructs PayPal to authorize (hold) <paramref name="Amount"/> against either a raw card or a
/// previously vaulted card. Exactly one of <see cref="Card"/> / <see cref="VaultId"/> must be set - the
/// caller is responsible for that validation before constructing this request.
/// </summary>
public record PayPalAuthorizeRequest(
    string EshopOrderId,
    decimal Amount,
    string Currency,
    PayPalCardDetails? Card,
    string? VaultId,
    string IdempotencyKey);
