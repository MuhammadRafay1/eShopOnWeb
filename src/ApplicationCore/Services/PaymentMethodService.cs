using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class PaymentMethodService : IPaymentMethodService
{
    private readonly IRepository<PaymentMethod> _repository;
    private readonly IPayPalVaultClient _vaultClient;
    private readonly IAppLogger<PaymentMethodService> _logger;

    public PaymentMethodService(
        IRepository<PaymentMethod> repository,
        IPayPalVaultClient vaultClient,
        IAppLogger<PaymentMethodService> logger)
    {
        _repository = repository;
        _vaultClient = vaultClient;
        _logger = logger;
    }

    public async Task<PaymentMethod> SaveCardAsync(string buyerId, PayPalCard card,
        CancellationToken cancellationToken = default)
    {
        // Reuse the PayPal customer id from an existing card so all of a buyer's cards land under
        // one PayPal customer; otherwise start a new customer keyed by a safe reference to the buyer.
        var existing = await _repository.ListAsync(new PaymentMethodsByBuyerSpecification(buyerId), cancellationToken);
        var existingCustomerId = existing.FirstOrDefault()?.PayPalCustomerId;

        var requestId = $"eshop-vault-{buyerId}-{Guid.NewGuid():N}";
        var result = await _vaultClient.VaultCardAsync(
            card, ToMerchantCustomerId(buyerId), existingCustomerId, requestId, cancellationToken);

        var paymentMethod = new PaymentMethod(
            payPalVaultId: result.VaultId,
            buyerId: buyerId,
            payPalCustomerId: result.CustomerId,
            brand: result.Brand,
            last4: result.Last4,
            expiryMonth: result.ExpiryMonth,
            expiryYear: result.ExpiryYear);

        await _repository.AddAsync(paymentMethod, cancellationToken);
        _logger.LogInformation("Saved card {0} for buyer (vault id).", result.VaultId);
        return paymentMethod;
    }

    public async Task<IReadOnlyList<PaymentMethod>> ListAsync(string buyerId,
        CancellationToken cancellationToken = default)
    {
        var cards = await _repository.ListAsync(new PaymentMethodsByBuyerSpecification(buyerId), cancellationToken);
        return cards;
    }

    public async Task<bool> DeleteAsync(string buyerId, string paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        var spec = new PaymentMethodByIdForBuyerSpecification(paymentMethodId, buyerId);
        var card = await _repository.FirstOrDefaultAsync(spec, cancellationToken);
        if (card is null) return false; // not found or not the caller's -> 404

        // Delete at PayPal first so the vault id itself stops working, then remove the local row.
        await _vaultClient.DeleteVaultedCardAsync(card.PayPalVaultId, cancellationToken);
        await _repository.DeleteAsync(card, cancellationToken);
        _logger.LogInformation("Deleted saved card {0}.", paymentMethodId);
        return true;
    }

    // The spec's pattern for merchant_customer_id nominally allows punctuation, but PayPal's
    // sandbox rejects some of it (e.g. '@' in an email) with a 500. Derive a stable, purely
    // alphanumeric id from a hash of the buyer identity so a buyer's first vaulted card always
    // maps to the same PayPal customer, whatever characters the identity string contains.
    private static string ToMerchantCustomerId(string buyerId)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(buyerId));
        var hex = System.Convert.ToHexString(hash).ToLowerInvariant();
        return "eshop" + hex.Substring(0, 32); // 37 chars, [0-9a-z], well within the 64 limit
    }
}
