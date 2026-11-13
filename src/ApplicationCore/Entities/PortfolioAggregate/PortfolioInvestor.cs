using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.ManagedPortfolio;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A shopper's investor record. One per shopper, keyed by the shopper's identity (the JWT name / email),
/// which is the primary key — so a second onboarding of the same shopper is rejected by the store. Holds the
/// Upvest identifiers obtained during onboarding and the shopper's model choice. No personal detail is stored
/// here beyond the shopper key; the personal data lives only at Upvest.
/// </summary>
public class PortfolioInvestor : IAggregateRoot
{
#pragma warning disable CS8618 // Required by Entity Framework
    private PortfolioInvestor() { }
#pragma warning restore CS8618

    public PortfolioInvestor(string shopperKey)
    {
        ShopperKey = Guard.Against.NullOrEmpty(shopperKey, nameof(shopperKey));
        CreatedAt = DateTimeOffset.UtcNow;
        LastKnownStatus = InvestorStatus.Pending;
    }

    /// <summary>The shopper's identity (JWT name). Primary key.</summary>
    public string ShopperKey { get; private set; }

    public Guid? UpvestUserId { get; private set; }
    public Guid? AccountGroupId { get; private set; }
    public Guid? AccountId { get; private set; }

    /// <summary>The operator model id the investor's portfolio follows, if chosen.</summary>
    public string? SelectedModelId { get; private set; }

    /// <summary>Whether the Upvest portfolio configuration linking the account to a model has been created.</summary>
    public bool ModelConfigured { get; private set; }

    public InvestorStatus LastKnownStatus { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void RecordUpvestIdentifiers(Guid userId, Guid? accountGroupId, Guid? accountId)
    {
        UpvestUserId = userId;
        if (accountGroupId.HasValue) AccountGroupId = accountGroupId;
        if (accountId.HasValue) AccountId = accountId;
    }

    public void UpdateStatus(InvestorStatus status) => LastKnownStatus = status;

    public void SelectModel(string modelId)
    {
        SelectedModelId = Guard.Against.NullOrEmpty(modelId, nameof(modelId));
    }

    public void MarkModelConfigured() => ModelConfigured = true;

    /// <summary>True once the shopper may pay in: accepted by Upvest with an account that can hold a portfolio.</summary>
    public bool IsReadyToInvest => LastKnownStatus == InvestorStatus.Active && AccountId.HasValue;
}
