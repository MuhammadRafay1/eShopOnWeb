using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Payments;

public class PaymentMethodService : IPaymentMethodService
{
    private readonly IRepository<SavedPaymentMethod> _savedPaymentMethodRepository;
    private readonly IRepository<ShopperPayPalProfile> _profileRepository;
    private readonly IPayPalPaymentGateway _gateway;

    public PaymentMethodService(
        IRepository<SavedPaymentMethod> savedPaymentMethodRepository,
        IRepository<ShopperPayPalProfile> profileRepository,
        IPayPalPaymentGateway gateway)
    {
        _savedPaymentMethodRepository = savedPaymentMethodRepository;
        _profileRepository = profileRepository;
        _gateway = gateway;
    }

    public async Task<SavedPaymentMethodView> SaveAsync(string buyerId, CardDetails card, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var profile = await _profileRepository.GetByIdAsync(buyerId, cancellationToken);
        var vaultResult = await _gateway.SaveCardAsync(
            new SaveCardCommand(card, profile?.PayPalCustomerId, buyerId), cancellationToken);

        if (profile is null)
        {
            try
            {
                await _profileRepository.AddAsync(new ShopperPayPalProfile(buyerId, vaultResult.PayPalCustomerId), cancellationToken);
            }
            catch
            {
                // A racing first save already recorded this buyer's PayPal customer id - nothing more to do;
                // the card we just vaulted is still ours by SavedPaymentMethod.BuyerId regardless of which
                // PayPal customer id the profile ended up pointing at.
            }
        }

        var saved = new SavedPaymentMethod(
            vaultResult.VaultId, buyerId, vaultResult.Brand, vaultResult.LastDigits, vaultResult.Expiry, vaultResult.CardholderName);
        await _savedPaymentMethodRepository.AddAsync(saved, cancellationToken);

        return new SavedPaymentMethodView(saved.Id, saved.Brand, saved.LastDigits, saved.Expiry, saved.CardholderName);
    }

    public async Task<IReadOnlyList<SavedPaymentMethodView>> ListAsync(string buyerId, CancellationToken cancellationToken)
    {
        var methods = await _savedPaymentMethodRepository.ListAsync(new SavedPaymentMethodsByBuyerSpec(buyerId), cancellationToken);
        return methods.Select(m => new SavedPaymentMethodView(m.Id, m.Brand, m.LastDigits, m.Expiry, m.CardholderName)).ToList();
    }

    public async Task DeleteAsync(string buyerId, string paymentMethodId, CancellationToken cancellationToken)
    {
        var saved = await _savedPaymentMethodRepository.GetByIdAsync(paymentMethodId, cancellationToken);
        if (saved is null || saved.BuyerId != buyerId)
        {
            throw new ResourceNotFoundException("SavedPaymentMethod", paymentMethodId);
        }

        await _gateway.DeleteCardAsync(saved.Id, cancellationToken);
        await _savedPaymentMethodRepository.DeleteAsync(saved, cancellationToken);
    }
}
