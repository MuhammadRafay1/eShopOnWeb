using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Saves and removes a shopper's reusable cards via PayPal's Vault. The card itself lives only in
/// PayPal; this app stores the durable vault-token id plus display-safe descriptors.
/// </summary>
public class PaymentMethodService : IPaymentMethodService
{
    private readonly IRepository<PaymentMethod> _repository;
    private readonly IPayPalClient _payPalClient;

    public PaymentMethodService(IRepository<PaymentMethod> repository, IPayPalClient payPalClient)
    {
        _repository = repository;
        _payPalClient = payPalClient;
    }

    public async Task<PaymentMethod> SaveCardAsync(string buyerId, CardDetails card)
    {
        // Two-step vault exchange: setup token → durable payment token.
        var setupToken = await _payPalClient.CreateSetupTokenAsync(card);
        var vaulted = await _payPalClient.CreatePaymentTokenAsync(setupToken.SetupTokenId);

        var paymentMethod = new PaymentMethod(buyerId, vaulted.VaultId, vaulted.Brand, vaulted.Last4Digits, vaulted.Expiry);
        return await _repository.AddAsync(paymentMethod);
    }

    public async Task<IReadOnlyList<PaymentMethod>> ListAsync(string buyerId)
    {
        return await _repository.ListAsync(new PaymentMethodsByBuyerIdSpec(buyerId));
    }

    public async Task DeleteAsync(int paymentMethodId, string buyerId)
    {
        var paymentMethod = await _repository.FirstOrDefaultAsync(
            new PaymentMethodByIdAndBuyerIdSpec(paymentMethodId, buyerId));
        if (paymentMethod is null)
            throw new ResourceNotFoundException($"Saved card {paymentMethodId} was not found.");

        // Remove from PayPal's vault first so it truly can no longer be used, then from our store.
        await _payPalClient.DeletePaymentTokenAsync(paymentMethod.PayPalVaultId);
        await _repository.DeleteAsync(paymentMethod);
    }
}
