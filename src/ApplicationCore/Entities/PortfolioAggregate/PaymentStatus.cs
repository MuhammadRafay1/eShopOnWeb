namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle of a pay-in. Always reflects the outcome at Upvest (top-up settlement + portfolio BUY order).
/// </summary>
public enum PaymentStatus
{
    /// <summary>The outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The money was credited and invested across the model's funds at Upvest.</summary>
    Invested = 1,

    /// <summary>The top-up or the investment order failed at Upvest.</summary>
    Failed = 2
}
