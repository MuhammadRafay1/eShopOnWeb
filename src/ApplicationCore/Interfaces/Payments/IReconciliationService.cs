using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

public interface IReconciliationService
{
    Task<ReconciliationReport> GetReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
