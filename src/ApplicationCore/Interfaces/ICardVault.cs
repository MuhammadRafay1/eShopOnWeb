using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Abstraction over the PCI-compliant card vault (PayPal Vault). Full card data never touches this app's DB.</summary>
public interface ICardVault
{
    /// <summary>Vault a card for later reuse. <paramref name="buyerReference"/> ties the token to the shopper at PayPal.</summary>
    Task<SavedCardResult> SaveCardAsync(CardDetails card, string buyerReference, CancellationToken cancellationToken);

    /// <summary>Remove a vaulted card. A 404 (already gone) is treated as idempotent success.</summary>
    Task DeletePaymentTokenAsync(string paymentTokenId, CancellationToken cancellationToken);
}
