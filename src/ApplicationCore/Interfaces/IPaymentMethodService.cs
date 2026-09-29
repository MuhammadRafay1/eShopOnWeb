using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Manages a shopper's saved cards (vaulted in PayPal, referenced locally).</summary>
public interface IPaymentMethodService
{
    /// <summary>Vault a card for the shopper and persist a safe local reference to it.</summary>
    Task<PaymentMethod> SaveAsync(string buyerId, CardDetails card);

    /// <summary>The caller's saved cards.</summary>
    Task<IReadOnlyList<PaymentMethod>> ListAsync(string buyerId);

    /// <summary>
    /// Delete a saved card. Throws <see cref="Exceptions.PaymentMethodNotFoundException"/> if it
    /// does not exist or belongs to another shopper. Also removes it from the PayPal vault.
    /// </summary>
    Task DeleteAsync(string buyerId, int paymentMethodId);
}
