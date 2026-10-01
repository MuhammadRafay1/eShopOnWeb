using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Saves, lists and deletes a shopper's cards. Cards are vaulted at PayPal; this app stores only the PayPal
/// payment-token id plus safe display fields. A saved card belongs to the shopper who saved it — every read and
/// write is scoped to the caller's own <see cref="Buyer"/>.
/// </summary>
public sealed class SavedCardService : ISavedCardService
{
    private readonly IRepository<Buyer> _buyerRepository;
    private readonly ICardVault _vault;
    private readonly IOperationLock _lock;
    private readonly IAppLogger<SavedCardService> _logger;

    public SavedCardService(
        IRepository<Buyer> buyerRepository,
        ICardVault vault,
        IOperationLock operationLock,
        IAppLogger<SavedCardService> logger)
    {
        _buyerRepository = buyerRepository;
        _vault = vault;
        _lock = operationLock;
        _logger = logger;
    }

    private static string LockKey(string buyerId) => $"buyer-{buyerId}";

    public async Task<PaymentMethod> SaveCardAsync(string buyerId, string? alias, CardDetails card, CancellationToken cancellationToken)
    {
        using var _ = await _lock.AcquireAsync(LockKey(buyerId), cancellationToken);

        var saved = await _vault.SaveCardAsync(card, buyerId, cancellationToken);

        var resolvedAlias = string.IsNullOrWhiteSpace(alias)
            ? BuildAlias(saved)
            : alias!.Trim();

        var buyer = await _buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(buyerId), cancellationToken);
        PaymentMethod method;
        if (buyer is null)
        {
            buyer = new Buyer(buyerId);
            method = buyer.AddPaymentMethod(resolvedAlias, saved.PaymentTokenId, saved.Last4, saved.Brand, saved.Expiry);
            await _buyerRepository.AddAsync(buyer, cancellationToken);
        }
        else
        {
            method = buyer.AddPaymentMethod(resolvedAlias, saved.PaymentTokenId, saved.Last4, saved.Brand, saved.Expiry);
            await _buyerRepository.UpdateAsync(buyer, cancellationToken);
        }

        _logger.LogInformation("Saved card for buyer (token ending {0}).", saved.Last4 ?? "????");
        return method;
    }

    public async Task<IReadOnlyList<PaymentMethod>> ListAsync(string buyerId, CancellationToken cancellationToken)
    {
        var buyer = await _buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(buyerId), cancellationToken);
        if (buyer is null)
        {
            return Array.Empty<PaymentMethod>();
        }
        return buyer.PaymentMethods.Where(m => !m.IsDeleted).ToList();
    }

    public async Task DeleteAsync(string buyerId, int paymentMethodId, CancellationToken cancellationToken)
    {
        using var _ = await _lock.AcquireAsync(LockKey(buyerId), cancellationToken);

        var buyer = await _buyerRepository.FirstOrDefaultAsync(new BuyerWithPaymentMethodsSpecification(buyerId), cancellationToken);
        var method = buyer?.PaymentMethods.FirstOrDefault(m => m.Id == paymentMethodId);
        if (method is null)
        {
            throw new PaymentMethodNotFoundException(paymentMethodId);
        }

        if (method.IsDeleted)
        {
            return; // idempotent: already removed
        }

        if (method.CardId is not null)
        {
            await _vault.DeletePaymentTokenAsync(method.CardId, cancellationToken);
        }

        method.MarkDeleted();
        await _buyerRepository.UpdateAsync(buyer!, cancellationToken);
        _logger.LogInformation("Deleted saved card {0} for buyer.", paymentMethodId);
    }

    private static string BuildAlias(SavedCardResult saved)
    {
        var brand = string.IsNullOrWhiteSpace(saved.Brand) ? "Card" : saved.Brand;
        var last4 = string.IsNullOrWhiteSpace(saved.Last4) ? string.Empty : $" ending {saved.Last4}";
        return $"{brand}{last4}";
    }
}
