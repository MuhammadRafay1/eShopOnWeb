namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Where a pay-in has got to at Upvest. <see cref="Pending"/> until the payment's outcome is
/// known, then <see cref="Invested"/> or <see cref="Failed"/>.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Invested = 1,
    Failed = 2
}
