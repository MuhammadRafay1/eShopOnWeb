using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Manages a shopper's saved cards. A card belongs to the shopper who saved it; one shopper must
/// never see, use, or delete another's.
/// </summary>
public interface ISavedCardService
{
    /// <summary>Vaults a card for the shopper and returns its id plus a safe description.</summary>
    Task<SavedCardView> SaveCardAsync(string buyerId, CardInput card, CancellationToken cancellationToken = default);

    /// <summary>The caller's saved cards.</summary>
    Task<IReadOnlyList<SavedCardView>> GetCardsAsync(string buyerId, CancellationToken cancellationToken = default);

    /// <summary>Removes a saved card so it no longer appears and can no longer be used to pay.</summary>
    Task DeleteCardAsync(string buyerId, int paymentMethodId, CancellationToken cancellationToken = default);
}
