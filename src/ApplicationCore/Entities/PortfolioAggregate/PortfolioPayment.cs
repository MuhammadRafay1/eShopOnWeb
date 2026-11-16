using System;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// A pay-in by an investor: cash credited to their Upvest account and invested across their model's
/// funds via a portfolio order. The reported status reflects the live state of that order at Upvest.
/// </summary>
public class PortfolioPayment : BaseEntity, IAggregateRoot
{
    public string ShopUserName { get; private set; }

    /// <summary>Amount paid in, in euros.</summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    /// <summary>The Upvest funding (top-up) that credits the cash, once created.</summary>
    public string? UpvestTopupId { get; private set; }
    public string? TopupStatus { get; private set; }

    /// <summary>The Upvest portfolio order that invests the cash across the model, once created.</summary>
    public string? UpvestOrderId { get; private set; }
    public string? OrderStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PortfolioPayment()
    {
        ShopUserName = default!;
        Currency = default!;
    }

    public PortfolioPayment(string shopUserName, decimal amount, string currency)
    {
        ShopUserName = Guard.Against.NullOrEmpty(shopUserName, nameof(shopUserName));
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be positive.");
        Amount = amount;
        Currency = Guard.Against.NullOrEmpty(currency, nameof(currency));
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void SetTopup(string topupId, string? status)
    {
        UpvestTopupId = topupId;
        TopupStatus = status;
        Touch();
    }

    public void SetOrder(string orderId, string? status)
    {
        UpvestOrderId = orderId;
        OrderStatus = status;
        Touch();
    }

    public void UpdateOrderStatus(string? status)
    {
        if (status is not null) OrderStatus = status;
        Touch();
    }

    public void UpdateTopupStatus(string? status)
    {
        if (status is not null) TopupStatus = status;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    /// <summary>
    /// Maps the live Upvest order state onto the payment status the API exposes:
    /// <c>pending</c> until the outcome is known, then <c>invested</c> or <c>failed</c>.
    /// </summary>
    public PortfolioPaymentStatus MapStatus()
    {
        // If the funding itself was cancelled the money never arrived -> failed.
        if (Equals(TopupStatus, "CANCELLED")) return PortfolioPaymentStatus.Failed;

        return OrderStatus?.ToUpperInvariant() switch
        {
            "FILLED" or "SETTLED" => PortfolioPaymentStatus.Invested,
            "CANCELLED" => PortfolioPaymentStatus.Failed,
            _ => PortfolioPaymentStatus.Pending
        };
    }

    private static bool Equals(string? a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
