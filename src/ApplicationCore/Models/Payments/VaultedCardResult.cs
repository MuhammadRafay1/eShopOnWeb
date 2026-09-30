namespace Microsoft.eShopWeb.ApplicationCore.Models.Payments;

/// <summary>A safe, post-vaulting card descriptor. PayPal's vault response never carries a PAN, so
/// this type structurally cannot either.</summary>
public class VaultedCardResult
{
    public string VaultId { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string LastDigits { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public string? CardholderName { get; set; }
    public string? PayPalCustomerId { get; set; }
}
