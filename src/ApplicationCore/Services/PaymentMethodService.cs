using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentMethodService : IPaymentMethodService
{
    private readonly IRepository<VaultedPaymentMethod> _repository;
    private readonly IReadRepository<VaultedPaymentMethod> _readRepository;
    private readonly IPayPalPaymentGateway _gateway;

    public PaymentMethodService(IRepository<VaultedPaymentMethod> repository, IReadRepository<VaultedPaymentMethod> readRepository, IPayPalPaymentGateway gateway)
    {
        _repository = repository;
        _readRepository = readRepository;
        _gateway = gateway;
    }

    public async Task<VaultedPaymentMethod> SaveCardAsync(string buyerId, PayPalCardInput card, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var existing = await _readRepository.ListAsync(new VaultedPaymentMethodsByBuyerSpec(buyerId), cancellationToken);
        var existingCustomerId = existing.Select(pm => pm.PayPalCustomerId).FirstOrDefault(id => id is not null);

        var vaultRequest = new PayPalVaultCardRequest(card, existingCustomerId, Guid.NewGuid().ToString("N"));
        var result = await _gateway.VaultCardAsync(vaultRequest, cancellationToken);

        var paymentMethod = new VaultedPaymentMethod(
            buyerId,
            result.VaultId,
            result.CustomerId,
            result.Brand,
            result.LastDigits,
            result.Expiry,
            result.CardholderName);

        return await _repository.AddAsync(paymentMethod, cancellationToken);
    }

    public async Task<IReadOnlyList<VaultedPaymentMethod>> GetForBuyerAsync(string buyerId, CancellationToken cancellationToken)
    {
        return await _readRepository.ListAsync(new VaultedPaymentMethodsByBuyerSpec(buyerId), cancellationToken);
    }

    public async Task<bool> DeleteAsync(string buyerId, int paymentMethodId, CancellationToken cancellationToken)
    {
        var paymentMethod = await _repository.FirstOrDefaultAsync(new VaultedPaymentMethodByIdSpec(paymentMethodId), cancellationToken);
        if (paymentMethod is null || paymentMethod.BuyerId != buyerId)
        {
            return false;
        }

        await _gateway.DeleteVaultedCardAsync(paymentMethod.PayPalVaultId, cancellationToken);
        await _repository.DeleteAsync(paymentMethod, cancellationToken);
        return true;
    }
}
