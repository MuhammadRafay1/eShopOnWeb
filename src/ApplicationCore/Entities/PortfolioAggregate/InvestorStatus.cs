namespace Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;

public enum InvestorStatus
{
    Pending,
    Active,
    Rejected
}

public enum PortfolioPaymentStatus
{
    Pending,
    Invested,
    Failed
}

public enum RealignmentStatus
{
    Pending,
    Completed,
    Failed
}
