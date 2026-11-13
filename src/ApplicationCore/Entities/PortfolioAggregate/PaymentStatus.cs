namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle status of a pay-in, mirroring the outcome at Upvest.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The payment's outcome at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The money was credited and invested across the model's funds at Upvest.</summary>
    Invested = 1,

    /// <summary>The payment failed at Upvest.</summary>
    Failed = 2
}
