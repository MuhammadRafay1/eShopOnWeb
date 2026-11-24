using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor. Records the amount and the Upvest resource ids (cash top-up and
/// portfolio order) whose live state determines the reported status.
/// </summary>
public class PortfolioPayment : BaseEntity, IAggregateRoot
{
    public Guid PublicId { get; private set; }
    public string ShopperUserName { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string? UpvestCashIncreaseId { get; private set; }
    public string? UpvestOrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private PortfolioPayment()
    {
        ShopperUserName = string.Empty;
        Currency = "EUR";
    }

    public PortfolioPayment(string shopperUserName, decimal amount, string currency)
    {
        ShopperUserName = Guard.Against.NullOrEmpty(shopperUserName, nameof(shopperUserName));
        Amount = Guard.Against.NegativeOrZero(amount, nameof(amount));
        Currency = Guard.Against.NullOrEmpty(currency, nameof(currency));
        PublicId = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void SetUpvestReferences(string? cashIncreaseId, string orderId)
    {
        UpvestCashIncreaseId = cashIncreaseId;
        UpvestOrderId = Guard.Against.NullOrEmpty(orderId, nameof(orderId));
    }
}
