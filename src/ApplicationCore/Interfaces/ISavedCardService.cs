using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public record SavedCardResult(int PaymentMethodId, string? Brand, string? Last4, string? Expiry, string? CardType, string? Label);

/// <summary>
/// Vaults a shopper's card with PayPal and keeps a local, ownership-scoped record of the safe
/// display fields PayPal returns. No PAN/CVV ever reaches this layer's storage.
/// </summary>
public interface ISavedCardService
{
    Task<SavedCardResult> SaveCardAsync(string buyerId, CardDetails card, string? label, CancellationToken ct = default);

    Task<IReadOnlyList<SavedCardResult>> ListCardsAsync(string buyerId, CancellationToken ct = default);

    Task DeleteCardAsync(string buyerId, int paymentMethodId, CancellationToken ct = default);
}
