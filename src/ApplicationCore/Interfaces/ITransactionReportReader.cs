using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Payments;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>Reads PayPal's own record of transactions for a date range, for reconciliation against eShop orders.</summary>
public interface ITransactionReportReader
{
    /// <summary>
    /// Search PayPal transactions over <paramref name="from"/>..<paramref name="to"/>. Covers the whole range
    /// (chunking the provider's 31-day window cap and paging each chunk), with a bounded result that signals
    /// truncation if a safety cap is hit.
    /// </summary>
    Task<TransactionSearchResult> SearchAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
