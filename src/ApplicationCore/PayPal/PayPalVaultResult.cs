namespace Microsoft.eShopWeb.ApplicationCore.PayPal;

public record PayPalVaultResult(
    string VaultId,
    string CustomerId,
    string Brand,
    string Last4,
    string Expiry);
