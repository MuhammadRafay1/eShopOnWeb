using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class SavedPaymentMethodService : ISavedPaymentMethodService
{
    private readonly IPayPalPaymentGateway _gateway;
    private readonly IRepository<SavedPaymentMethod> _repository;
    private readonly IAppLogger<SavedPaymentMethodService> _logger;

    public SavedPaymentMethodService(
        IPayPalPaymentGateway gateway,
        IRepository<SavedPaymentMethod> repository,
        IAppLogger<SavedPaymentMethodService> logger)
    {
        _gateway = gateway;
        _repository = repository;
        _logger = logger;
    }

    public async Task<SavedPaymentMethod> SaveCardAsync(string buyerId, CardDetails card, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.Null(card, nameof(card));

        // Fresh idempotency key per call — saving a card is not something a caller retries with the same key.
        var vaulted = await _gateway.CreateVaultedCardAsync(Guid.NewGuid().ToString("N"), buyerId, card, ct);

        var saved = new SavedPaymentMethod(
            buyerId, vaulted.VaultId, vaulted.Brand, vaulted.LastFour, vaulted.Expiry, card.CardholderName);

        saved = await _repository.AddAsync(saved, ct);
        _logger.LogInformation("Saved payment method {0} ({1} ****{2}) for a shopper.",
            saved.Id, saved.CardBrand, saved.LastFour);
        return saved;
    }

    public async Task<IReadOnlyList<SavedPaymentMethod>> ListAsync(string buyerId, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        return await _repository.ListAsync(new SavedPaymentMethodsByBuyerIdSpecification(buyerId), ct);
    }

    public async Task DeleteAsync(string buyerId, int paymentMethodId, CancellationToken ct)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var saved = await _repository.GetByIdAsync(paymentMethodId, ct);
        if (saved is null || saved.BuyerId != buyerId)
        {
            // 404-not-403: never reveal another shopper's card exists.
            throw new PaymentMethodNotFoundException(paymentMethodId);
        }

        // Revoke PayPal-side FIRST. If this throws, do not delete the local row — otherwise a card that
        // failed to revoke would show as removed locally while remaining chargeable state on PayPal.
        await _gateway.DeleteVaultedCardAsync(saved.PayPalVaultId, ct);
        await _repository.DeleteAsync(saved, ct);
        _logger.LogInformation("Removed saved payment method {0}.", paymentMethodId);
    }
}
