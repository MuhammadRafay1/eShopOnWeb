using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Save / list / delete a shopper's vaulted cards.</summary>
public interface IPaymentMethodService
{
    Task<SavedCardSummary> SaveAsync(string buyerId, CardDetails card, CancellationToken ct = default);

    Task<IReadOnlyList<SavedCardSummary>> ListAsync(string buyerId, CancellationToken ct = default);

    /// <summary>Deletes the caller's own saved card. Returns false if it does not exist or belongs to someone else.</summary>
    Task<bool> DeleteAsync(string buyerId, int paymentMethodId, CancellationToken ct = default);
}
