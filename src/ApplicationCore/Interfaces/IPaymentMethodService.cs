using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IPaymentMethodService
{
    /// <summary>Vault a card for the shopper and store display-safe descriptors (never the card number).</summary>
    Task<PaymentMethod> SaveCardAsync(string buyerId, CardDetails card);

    /// <summary>The caller's own saved cards.</summary>
    Task<IReadOnlyList<PaymentMethod>> ListAsync(string buyerId);

    /// <summary>Remove a saved card (owner-scoped); afterwards it can no longer be used to pay.</summary>
    Task DeleteAsync(int paymentMethodId, string buyerId);
}
