using System.Collections.Generic;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class SavedCardService : ISavedCardService
{
    private readonly IRepository<SavedPaymentMethod> _repository;
    private readonly IPayPalGateway _payPal;

    public SavedCardService(IRepository<SavedPaymentMethod> repository, IPayPalGateway payPal)
    {
        _repository = repository;
        _payPal = payPal;
    }

    public async Task<Result<SavedPaymentMethod>> SaveCardAsync(string buyerId, PayPalCardDetails card, string? label)
    {
        var vaulted = await _payPal.VaultCardAsync(card);

        var savedCard = new SavedPaymentMethod(buyerId, vaulted.VaultId, vaulted.CustomerId, vaulted.Brand, vaulted.Last4, vaulted.Expiry, label);
        await _repository.AddAsync(savedCard);

        return Result<SavedPaymentMethod>.Success(savedCard);
    }

    public async Task<IReadOnlyList<SavedPaymentMethod>> ListAsync(string buyerId)
    {
        return await _repository.ListAsync(new SavedCardsByBuyerSpecification(buyerId));
    }

    public async Task<Result> DeleteAsync(int paymentMethodId, string buyerId)
    {
        var savedCard = await _repository.FirstOrDefaultAsync(new SavedCardByIdAndBuyerSpecification(paymentMethodId, buyerId));
        if (savedCard is null)
        {
            return Result.NotFound();
        }

        await _payPal.DeleteVaultedCardAsync(savedCard.PayPalVaultId);
        await _repository.DeleteAsync(savedCard);

        return Result.Success();
    }
}
