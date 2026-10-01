using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Manages a shopper's saved cards (vaulted at PayPal; only safe display fields stored locally).</summary>
public interface ISavedCardService
{
    Task<PaymentMethod> SaveCardAsync(string buyerId, string? alias, CardDetails card, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentMethod>> ListAsync(string buyerId, CancellationToken cancellationToken);

    Task DeleteAsync(string buyerId, int paymentMethodId, CancellationToken cancellationToken);
}
