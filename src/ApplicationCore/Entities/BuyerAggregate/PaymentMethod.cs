using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

/// <summary>
/// A shopper's saved card. Full card data is never stored here — only the PayPal vault token id
/// (<see cref="CardId"/>) plus a safe display description (brand / last 4 / expiry). The actual PAN
/// lives only in PayPal's PCI-compliant vault.
/// </summary>
public class PaymentMethod : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }

    public PaymentMethod(string vaultId, string brand, string last4, string expiryYearMonth, string? alias)
    {
        Guard.Against.NullOrEmpty(vaultId, nameof(vaultId));
        CardId = vaultId;
        Brand = brand;
        Last4 = last4;
        ExpiryYearMonth = expiryYearMonth;
        Alias = alias;
    }

    public int BuyerId { get; private set; }

    public string? Alias { get; private set; }

    /// <summary>The PayPal vault token id. Actual card data is stored in PayPal's PCI-compliant vault.</summary>
    public string? CardId { get; private set; }

    /// <summary>Card brand (VISA, MASTERCARD, ...), from the vault response's card_response_entity.brand.</summary>
    public string? Brand { get; private set; }

    public string? Last4 { get; private set; }

    /// <summary>Expiry in YYYY-MM form, from card_response_entity.expiry.</summary>
    public string? ExpiryYearMonth { get; private set; }
}
