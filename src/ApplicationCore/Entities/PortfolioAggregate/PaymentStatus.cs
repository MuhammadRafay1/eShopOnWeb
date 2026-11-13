namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Outcome of a pay-in at Upvest. <see cref="Pending"/> until the payment's outcome at
/// Upvest is known, then <see cref="Invested"/> or <see cref="Failed"/>.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Invested = 1,
    Failed = 2
}
