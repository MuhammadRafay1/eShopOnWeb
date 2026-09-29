using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

/// <summary>
/// Transaction reporting against PayPal's Reporting v1 API. Implementations transparently chunk
/// the requested range into PayPal's maximum 31-day windows and paginate fully within each, so
/// callers get the whole range, not just the first page.
/// </summary>
public interface IPayPalReconciliationGateway
{
    Task<IReadOnlyList<PayPalTransactionRecord>> SearchTransactionsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
