using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class SavedCardService : ISavedCardService
{
    private readonly IRepository<SavedCard> _savedCardRepository;
    private readonly IPaymentGateway _gateway;

    public SavedCardService(IRepository<SavedCard> savedCardRepository, IPaymentGateway gateway)
    {
        _savedCardRepository = savedCardRepository;
        _gateway = gateway;
    }

    public async Task<SavedCard> SaveCardAsync(string buyerId, CardInput card, CancellationToken ct)
    {
        var merchantCustomerId = $"eshop-{Sanitize(buyerId)}";
        var vaultCard = await _gateway.CreateVaultCardAsync(card, merchantCustomerId, $"vault-{Sanitize(buyerId)}-{Guid.NewGuid():N}", ct);

        var savedCard = new SavedCard(buyerId, vaultCard.VaultId, vaultCard.Brand, vaultCard.LastDigits, vaultCard.Expiry, card.CardholderName);
        return await _savedCardRepository.AddAsync(savedCard, ct);
    }

    public async Task<IReadOnlyList<SavedCard>> GetCardsForBuyerAsync(string buyerId, CancellationToken ct)
        => await _savedCardRepository.ListAsync(new SavedCardsByBuyerSpec(buyerId), ct);

    public async Task<bool> DeleteCardAsync(int paymentMethodId, string buyerId, CancellationToken ct)
    {
        var card = await _savedCardRepository.FirstOrDefaultAsync(new SavedCardByIdForBuyerSpec(paymentMethodId, buyerId), ct);
        if (card is null)
        {
            return false;
        }

        await _gateway.DeleteVaultCardAsync(card.PayPalVaultId, ct);
        await _savedCardRepository.DeleteAsync(card, ct);
        return true;
    }

    private static string Sanitize(string buyerId) => new(buyerId.Where(char.IsLetterOrDigit).ToArray());
}
