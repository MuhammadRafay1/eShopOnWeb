namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Lifecycle of a pay-in, as reflected by Upvest.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The outcome of the pay-in at Upvest is not yet known.</summary>
    Pending = 0,

    /// <summary>The money has been credited and invested across the model's funds.</summary>
    Invested = 1,

    /// <summary>The pay-in failed at Upvest.</summary>
    Failed = 2
}
