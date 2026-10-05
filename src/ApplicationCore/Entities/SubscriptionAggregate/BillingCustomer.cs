using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.SubscriptionAggregate;

/// <summary>
/// Links an eShopOnWeb buyer to its customer record in the billing system.
/// The row is inserted (keyed by <see cref="BuyerId"/>) BEFORE the provider customer is created, so it doubles
/// as the claim that stops two concurrent requests from creating two provider customers.
/// </summary>
public class BillingCustomer : IAggregateRoot
{
    public string BuyerId { get; private set; }

    /// <summary>Deterministic reference sent to the billing system; unique per site at the provider.</summary>
    public string CustomerReference { get; private set; }

    /// <summary>The billing system's customer id; null while the claim is pending.</summary>
    public int? ProviderCustomerId { get; private set; }

    public DateTimeOffset ClaimedAt { get; private set; }

    public DateTimeOffset? ProvisionedAt { get; private set; }

    public bool IsProvisioned => ProviderCustomerId.HasValue;

    #pragma warning disable CS8618 // Required by Entity Framework
    private BillingCustomer() { }

    public BillingCustomer(string buyerId, string customerReference, DateTimeOffset claimedAt)
    {
        Guard.Against.NullOrWhiteSpace(buyerId, nameof(buyerId));
        Guard.Against.NullOrWhiteSpace(customerReference, nameof(customerReference));
        BuyerId = buyerId;
        CustomerReference = customerReference;
        ClaimedAt = claimedAt;
    }

    public bool IsStale(DateTimeOffset now, TimeSpan claimTimeToLive) =>
        !IsProvisioned && now - ClaimedAt > claimTimeToLive;

    public void MarkProvisioned(int providerCustomerId, DateTimeOffset now)
    {
        Guard.Against.NegativeOrZero(providerCustomerId, nameof(providerCustomerId));
        ProviderCustomerId = providerCustomerId;
        ProvisionedAt = now;
    }
}
