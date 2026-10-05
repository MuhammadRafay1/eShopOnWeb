using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;

/// <summary>
/// A card a shopper saved for later orders. The card itself lives in the payment provider's vault; this row
/// only keeps the provider's token id and what the shopper needs to recognise the card (brand, last digits,
/// expiry) — never the card number or security code.
/// </summary>
public class SavedPaymentMethod : BaseEntity, IAggregateRoot
{
    #pragma warning disable CS8618 // Required by Entity Framework
    private SavedPaymentMethod() { }

    public SavedPaymentMethod(string buyerId, string provider, string vaultTokenId, string? providerCustomerId,
        string? brand, string? lastDigits, string? expiry, string? nickname, DateTimeOffset createdAt)
    {
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));
        Guard.Against.NullOrEmpty(vaultTokenId, nameof(vaultTokenId));

        BuyerId = buyerId;
        Provider = provider;
        VaultTokenId = vaultTokenId;
        ProviderCustomerId = providerCustomerId;
        Brand = brand;
        LastDigits = lastDigits;
        Expiry = expiry;
        Nickname = nickname;
        CreatedAt = createdAt;
    }

    public string BuyerId { get; private set; }
    public string Provider { get; private set; }
    public string VaultTokenId { get; private set; }
    public string? ProviderCustomerId { get; private set; }
    public string? Brand { get; private set; }
    public string? LastDigits { get; private set; }
    public string? Expiry { get; private set; }
    public string? Nickname { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Set when the shopper removes the card; a removed card is never listed or usable again.</summary>
    public DateTimeOffset? RemovedAt { get; private set; }

    /// <summary>True while the provider-side vault token still has to be deleted (provider was unreachable).</summary>
    public bool ProviderDeletionPending { get; private set; }

    public bool IsActive => RemovedAt is null;

    public void Remove(DateTimeOffset at)
    {
        RemovedAt ??= at;
        ProviderDeletionPending = true;
    }

    public void MarkProviderDeleted() => ProviderDeletionPending = false;
}
