using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>Save / list / delete a shopper's vaulted cards.</summary>
public class PaymentMethodService : IPaymentMethodService
{
    private readonly IRepository<SavedPaymentMethod> _repository;
    private readonly IPaymentGateway _gateway;

    public PaymentMethodService(IRepository<SavedPaymentMethod> repository, IPaymentGateway gateway)
    {
        _repository = repository;
        _gateway = gateway;
    }

    public async Task<SavedCardSummary> SaveAsync(string buyerId, CardDetails card, CancellationToken ct = default)
    {
        var vaulted = await _gateway.VaultCardAsync(card, ct);

        var savedCard = new SavedPaymentMethod(
            buyerId,
            vaulted.VaultId,
            vaulted.Brand,
            vaulted.LastDigits,
            vaulted.Expiry,
            vaulted.CardholderName,
            vaulted.PayPalCustomerId);

        await _repository.AddAsync(savedCard, ct);

        return ToSummary(savedCard);
    }

    public async Task<IReadOnlyList<SavedCardSummary>> ListAsync(string buyerId, CancellationToken ct = default)
    {
        var cards = await _repository.ListAsync(new SavedPaymentMethodsByBuyerSpec(buyerId), ct);
        return cards.Select(ToSummary).ToList();
    }

    public async Task<bool> DeleteAsync(string buyerId, int paymentMethodId, CancellationToken ct = default)
    {
        var card = await _repository.GetByIdAsync(paymentMethodId, ct);
        if (card is null || card.BuyerId != buyerId)
        {
            return false;
        }

        await _gateway.DeleteVaultedCardAsync(card.VaultId, ct);
        await _repository.DeleteAsync(card, ct);
        return true;
    }

    private static SavedCardSummary ToSummary(SavedPaymentMethod card) => new()
    {
        PaymentMethodId = card.Id,
        Brand = card.Brand,
        LastDigits = card.LastDigits,
        Expiry = card.Expiry,
        CardholderName = card.CardholderName
    };
}
