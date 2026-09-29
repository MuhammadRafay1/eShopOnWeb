using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

/// <summary>
/// A card a shopper has saved for reuse. No full card details are ever stored here - only the
/// PayPal vault id (which represents the card at PayPal) plus a safe descriptor (brand, last 4,
/// expiry) so the shopper can recognise which card it is.
/// </summary>
public class PaymentMethod : BaseEntity
{
    public int BuyerId { get; private set; }
    public string? Alias { get; private set; }

    /// <summary>
    /// The PayPal vault id for this saved card. (The property was originally earmarked for
    /// "actual card data stored in a PCI compliant system" - this is exactly that: an opaque
    /// reference held by PayPal, never the PAN.)
    /// </summary>
    public string CardId { get; private set; }
    public string Last4 { get; private set; }
    public string Brand { get; private set; }

    /// <summary>Card expiry in PayPal's "YYYY-MM" format, echoed back safely from the vault.</summary>
    public string ExpiryYearMonth { get; private set; }

#pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }
#pragma warning restore CS8618

    public PaymentMethod(string cardId, string last4, string brand, string expiryYearMonth, string? alias)
    {
        Guard.Against.NullOrEmpty(cardId, nameof(cardId));
        Guard.Against.NullOrEmpty(last4, nameof(last4));

        CardId = cardId;
        Last4 = last4;
        Brand = brand;
        ExpiryYearMonth = expiryYearMonth;
        Alias = alias;
    }
}
