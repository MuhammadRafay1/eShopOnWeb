using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

/// <summary>Wraps the Transaction Search v1 API for the reconciliation report.</summary>
public interface IPayPalTransactionSearchClient
{
    /// <summary>
    /// GET /v1/reporting/transactions across [from, to]. The 31-day-per-request range limit and
    /// pagination (loop all pages) are handled internally, so a caller-supplied range of any width
    /// is fully covered.
    /// </summary>
    Task<IReadOnlyList<PayPalTransactionRecord>> SearchAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
