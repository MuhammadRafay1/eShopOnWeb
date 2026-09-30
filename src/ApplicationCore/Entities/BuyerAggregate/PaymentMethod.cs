using Ardalis.GuardClauses;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;

public class PaymentMethod : BaseEntity
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private PaymentMethod() { }

    public PaymentMethod(string cardId, string? last4, string? brand, string? expiry, string? alias)
    {
        Guard.Against.NullOrEmpty(cardId, nameof(cardId)); // actual card data must be stored in a PCI compliant system - this is the PayPal Vault token id
        CardId = cardId;
        Last4 = last4;
        Brand = brand;
        Expiry = expiry;
        Alias = alias;
    }

    public string? Alias { get; private set; }
    public string? CardId { get; private set; } // PayPal Vault payment token id
    public string? Last4 { get; private set; }
    public string? Brand { get; private set; }
    public string? Expiry { get; private set; } // YYYY-MM
}
