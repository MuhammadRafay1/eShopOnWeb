using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Orchestrates saving, listing and deleting a shopper's vaulted cards.</summary>
public interface IPaymentMethodService
{
    Task<VaultedPaymentMethod> SaveCardAsync(string buyerId, PayPalCardInput card, CancellationToken cancellationToken);

    Task<IReadOnlyList<VaultedPaymentMethod>> GetForBuyerAsync(string buyerId, CancellationToken cancellationToken);

    /// <summary>Deletes the saved card both locally and from PayPal's vault. No-op result if the caller does not own it (caller should 404).</summary>
    Task<bool> DeleteAsync(string buyerId, int paymentMethodId, CancellationToken cancellationToken);
}
