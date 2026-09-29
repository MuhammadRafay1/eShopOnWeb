using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;

/// <summary>
/// Saved-card vaulting against PayPal's Vault v3 API.
/// </summary>
public interface IPayPalVaultGateway
{
    /// <summary>Vaults a card for the given caller-owned customer id, returning a safe descriptor.</summary>
    Task<PayPalSavedCard> SaveCardAsync(string customerId, CardDetails card, CancellationToken ct);

    /// <summary>Removes a vaulted card from PayPal.</summary>
    Task DeletePaymentTokenAsync(string vaultId, CancellationToken ct);
}
