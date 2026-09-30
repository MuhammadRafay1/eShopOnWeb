using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class SavedCardService : ISavedCardService
{
    private readonly IRepository<SavedCard> _savedCardRepository;
    private readonly IPayPalClient _payPalClient;

    public SavedCardService(IRepository<SavedCard> savedCardRepository, IPayPalClient payPalClient)
    {
        _savedCardRepository = savedCardRepository;
        _payPalClient = payPalClient;
    }

    public async Task<SavedCard> SaveCardAsync(string buyerId, PayPalCardDetails card, string idempotencyKey,
        CancellationToken ct = default)
    {
        var setupToken = await _payPalClient.CreateSetupTokenAsync(card, $"{idempotencyKey}-setup", ct);
        var paymentToken = await _payPalClient.CreatePaymentTokenAsync(setupToken.SetupTokenId, $"{idempotencyKey}-token", ct);

        var brand = paymentToken.CardBrand ?? setupToken.CardBrand ?? "UNKNOWN";
        var last4 = paymentToken.CardLast4 ?? setupToken.CardLast4 ?? "0000";
        var expiry = paymentToken.Expiry ?? setupToken.Expiry ?? card.ExpiryYearMonth;

        var savedCard = new SavedCard(buyerId, paymentToken.VaultId, paymentToken.CustomerId, brand, last4, expiry);
        return await _savedCardRepository.AddAsync(savedCard, ct);
    }

    public async Task<IReadOnlyList<SavedCard>> ListCardsAsync(string buyerId, CancellationToken ct = default) =>
        await _savedCardRepository.ListAsync(new SavedCardsByBuyerSpecification(buyerId), ct);

    public async Task<bool> DeleteCardAsync(int paymentMethodId, string buyerId, CancellationToken ct = default)
    {
        var card = await _savedCardRepository.FirstOrDefaultAsync(
            new SavedCardByIdAndBuyerSpecification(paymentMethodId, buyerId), ct);
        if (card is null) return false;

        try
        {
            await _payPalClient.DeletePaymentTokenAsync(card.PayPalVaultId, ct);
        }
        catch (Exceptions.PayPalOperationException)
        {
            // Already gone from the vault, or PayPal is briefly unavailable - deleting our own
            // record is what actually makes the card unusable through this API, so proceed.
        }

        await _savedCardRepository.DeleteAsync(card, ct);
        return true;
    }
}
