using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface ISavedCardService
{
    Task<SavedCard> SaveCardAsync(string buyerId, PayPalCardDetails card, string idempotencyKey,
        CancellationToken ct = default);

    Task<IReadOnlyList<SavedCard>> ListCardsAsync(string buyerId, CancellationToken ct = default);

    /// <summary>Returns false when the card does not exist or is not owned by <paramref name="buyerId"/>.</summary>
    Task<bool> DeleteCardAsync(int paymentMethodId, string buyerId, CancellationToken ct = default);
}
