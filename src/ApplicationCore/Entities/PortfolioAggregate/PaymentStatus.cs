namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

/// <summary>
/// Where a pay-in has got to at Upvest: <c>pending</c> until the outcome of the invest order is known,
/// then <c>invested</c> or <c>failed</c>.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Invested = 1,
    Failed = 2
}
