using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class SavedCardService : ISavedCardService
{
    private readonly IRepository<SavedCard> _savedCardRepository;
    private readonly IPayPalGateway _gateway;
    private readonly string _instanceId;

    public SavedCardService(IRepository<SavedCard> savedCardRepository, IPayPalGateway gateway, string instanceId)
    {
        _savedCardRepository = savedCardRepository;
        _gateway = gateway;
        _instanceId = instanceId;
    }

    public async Task<SavedCardResult> SaveCardAsync(string buyerId, CardDetails card, string? label, CancellationToken ct = default)
    {
        var existingCards = await _savedCardRepository.ListAsync(new SavedCardsByBuyerSpec(buyerId), ct);
        var existingCustomerId = existingCards.Select(c => c.PayPalCustomerId).FirstOrDefault(id => !string.IsNullOrEmpty(id));

        var requestId = $"savecard-{_instanceId}-{Guid.NewGuid():N}";
        var saved = await _gateway.SaveCardAsync(card, existingCustomerId, requestId, ct);

        var savedCard = new SavedCard(buyerId, saved.VaultId, saved.CustomerId ?? existingCustomerId, saved.Brand, saved.Last4, saved.Expiry, saved.CardType, label);
        savedCard = await _savedCardRepository.AddAsync(savedCard, ct);

        return Map(savedCard);
    }

    public async Task<IReadOnlyList<SavedCardResult>> ListCardsAsync(string buyerId, CancellationToken ct = default)
    {
        var cards = await _savedCardRepository.ListAsync(new SavedCardsByBuyerSpec(buyerId), ct);
        return cards.Select(Map).ToList();
    }

    public async Task DeleteCardAsync(string buyerId, int paymentMethodId, CancellationToken ct = default)
    {
        var card = await _savedCardRepository.FirstOrDefaultAsync(new SavedCardByIdForBuyerSpec(paymentMethodId, buyerId), ct)
            ?? throw new NotFoundException($"No saved card {paymentMethodId} found for this shopper.");

        await _gateway.DeleteCardAsync(card.PayPalVaultId, ct);
        await _savedCardRepository.DeleteAsync(card, ct);
    }

    private static SavedCardResult Map(SavedCard card) => new(card.Id, card.Brand, card.Last4, card.Expiry, card.CardType, card.Label);
}
