using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

public interface IPaymentMethodService
{
    Task<SavedPaymentMethodView> SaveAsync(string buyerId, CardDetails card, CancellationToken cancellationToken);

    Task<IReadOnlyList<SavedPaymentMethodView>> ListAsync(string buyerId, CancellationToken cancellationToken);

    Task DeleteAsync(string buyerId, string paymentMethodId, CancellationToken cancellationToken);
}
