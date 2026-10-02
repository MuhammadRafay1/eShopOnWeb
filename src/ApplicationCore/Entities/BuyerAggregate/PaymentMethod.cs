namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

public class PaymentMethod : BaseEntity
{
    public int BuyerId { get; private set; }

    /// <summary>
    /// The PayPal-generated vault id for this saved card. This, plus the display fields below, is all
    /// that is ever stored here — full card details live only in PayPal's PCI-compliant vault.
    /// </summary>
    public string PayPalVaultId { get; private set; }
    public string? Brand { get; private set; }
    public string? Last4 { get; private set; }
    public string? Expiry { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }

    public PaymentMethod(int buyerId, string payPalVaultId, string? brand, string? last4, string? expiry)
    {
        BuyerId = buyerId;
        PayPalVaultId = payPalVaultId;
        Brand = brand;
        Last4 = last4;
        Expiry = expiry;
    }

    public string Describe() => $"{Brand ?? "Card"} ending in {Last4 ?? "****"}";
}
