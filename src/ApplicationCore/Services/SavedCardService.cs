using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class SavedCardService : ISavedCardService
{
    private readonly IRepository<SavedCard> _repository;
    private readonly IPaymentGateway _gateway;
    private readonly IPaymentLock _lock;
    private readonly IAppLogger<SavedCardService> _logger;

    public SavedCardService(
        IRepository<SavedCard> repository,
        IPaymentGateway gateway,
        IPaymentLock paymentLock,
        IAppLogger<SavedCardService> logger)
    {
        _repository = repository;
        _gateway = gateway;
        _lock = paymentLock;
        _logger = logger;
    }

    private static string BuyerGate(string buyerId) => $"buyer:{buyerId}";

    public async Task<SavedCardView> SaveCardAsync(string buyerId, CardInput card, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        ValidateCard(card);

        using var gate = await _lock.AcquireAsync(BuyerGate(buyerId), cancellationToken);

        var existing = await _repository.ListAsync(new SavedCardsByBuyerSpecification(buyerId), cancellationToken);
        // All of a shopper's cards share one PayPal customer; reuse it when they already have one.
        var customerId = existing.FirstOrDefault()?.PayPalCustomerId;

        var request = new VaultCardGatewayRequest
        {
            Card = card,
            ExistingPayPalCustomerId = customerId,
            MerchantCustomerId = DeriveMerchantCustomerId(buyerId),
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        var vaulted = await _gateway.VaultCardAsync(request, cancellationToken);

        // If PayPal returned a vault id we already hold for this buyer, do not store a duplicate.
        var duplicate = existing.FirstOrDefault(c => c.PayPalVaultId == vaulted.VaultId);
        if (duplicate is not null)
        {
            return ToView(duplicate);
        }

        var saved = new SavedCard(buyerId, vaulted.VaultId, vaulted.PayPalCustomerId,
            vaulted.Brand, vaulted.LastFourDigits, vaulted.Expiry, vaulted.CardholderName);
        saved = await _repository.AddAsync(saved, cancellationToken);
        _logger.LogInformation($"Saved card {saved.Id} for {buyerId}: {vaulted.Brand} ****{vaulted.LastFourDigits}.");
        return ToView(saved);
    }

    public async Task<IReadOnlyList<SavedCardView>> GetCardsAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        var cards = await _repository.ListAsync(new SavedCardsByBuyerSpecification(buyerId), cancellationToken);
        return cards.OrderByDescending(c => c.CreatedAt).Select(ToView).ToList();
    }

    public async Task DeleteCardAsync(string buyerId, int paymentMethodId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        using var gate = await _lock.AcquireAsync(BuyerGate(buyerId), cancellationToken);

        var card = await _repository.GetByIdAsync(paymentMethodId, cancellationToken);
        if (card is null || card.BuyerId != buyerId)
        {
            // Surfaced as "not found" so one shopper cannot see or delete another's card.
            throw new PaymentNotFoundException($"Payment method {paymentMethodId} was not found.");
        }

        try
        {
            await _gateway.DeleteVaultedCardAsync(card.PayPalVaultId, cancellationToken);
        }
        catch (PaymentGatewayException ex) when (ex.ProviderStatusCode == 404)
        {
            // Already gone at PayPal — fine; remove our record too.
            _logger.LogWarning($"Vault token for card {paymentMethodId} was already absent at PayPal; removing local record.");
        }

        await _repository.DeleteAsync(card, cancellationToken);
        _logger.LogInformation($"Deleted saved card {paymentMethodId} for {buyerId}.");
    }

    private static SavedCardView ToView(SavedCard c) =>
        new(c.Id, c.Brand, c.LastFourDigits, c.Expiry, c.CardholderName, c.CreatedAt);

    private static void ValidateCard(CardInput card)
    {
        if (card is null || string.IsNullOrWhiteSpace(card.Number) || string.IsNullOrWhiteSpace(card.Expiry) || string.IsNullOrWhiteSpace(card.SecurityCode))
        {
            throw new PaymentValidationException("Card number, expiry (YYYY-MM) and security code are required.");
        }
    }

    /// <summary>
    /// Produces a PayPal merchant_customer_id (allowed: 0-9 a-z A-Z - _ . ^ * $ @ #, max 64) from the buyer id.
    /// </summary>
    private static string DeriveMerchantCustomerId(string buyerId)
    {
        var sb = new StringBuilder(buyerId.Length);
        foreach (var ch in buyerId)
        {
            sb.Append(IsAllowedMerchantCustomerChar(ch) ? ch : '_');
        }
        var sanitized = sb.ToString();
        return sanitized.Length <= 64 ? sanitized : sanitized.Substring(0, 64);
    }

    private static bool IsAllowedMerchantCustomerChar(char ch) =>
        char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or '^' or '*' or '$' or '@' or '#';
}
