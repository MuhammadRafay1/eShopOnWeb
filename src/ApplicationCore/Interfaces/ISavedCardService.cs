using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface ISavedCardService
{
    Task<SavedCard> SaveCardAsync(string buyerId, CardInput card, CancellationToken ct);

    Task<IReadOnlyList<SavedCard>> GetCardsForBuyerAsync(string buyerId, CancellationToken ct);

    /// <summary>Returns false if the card does not exist or is not owned by buyerId.</summary>
    Task<bool> DeleteCardAsync(int paymentMethodId, string buyerId, CancellationToken ct);
}
