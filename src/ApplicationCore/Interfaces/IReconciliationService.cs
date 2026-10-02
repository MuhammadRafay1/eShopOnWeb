using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Builds the reconciliation report that lines PayPal's own record of transactions up against eShop
/// orders for a date range, so a payment one side knows about and the other does not is visible.
/// </summary>
public interface IReconciliationService
{
    Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}
