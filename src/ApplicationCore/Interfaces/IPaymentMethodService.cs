using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Save, list and remove a shopper's vaulted cards. Every operation is caller-scoped.</summary>
public interface IPaymentMethodService
{
    /// <summary>Vaults a card at PayPal and records a display-safe copy against the shopper.</summary>
    Task<PaymentMethod> SaveCardAsync(string buyerId, PayPalCard card,
        CancellationToken cancellationToken = default);

    /// <summary>The caller's own saved cards.</summary>
    Task<IReadOnlyList<PaymentMethod>> ListAsync(string buyerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a saved card at PayPal and locally. Returns false if the card does not exist or
    /// does not belong to the caller (the API turns that into a 404).
    /// </summary>
    Task<bool> DeleteAsync(string buyerId, string paymentMethodId,
        CancellationToken cancellationToken = default);
}
