using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Duplicate-prevention claim store for payment writes (authorize/capture/void/refund). A caller takes
/// the claim before making the PayPal call; a second caller for the same logical operation is refused
/// by the store's own primary-key conflict, not by a read-then-write race.
/// </summary>
public interface IPaymentOperationClaimStore
{
    /// <summary>Attempts to atomically claim <paramref name="claimKey"/>. Returns true if this caller won the claim; false if it was already claimed.</summary>
    Task<bool> TryClaimAsync(string claimKey, CancellationToken cancellationToken);
}
