namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// Space-delimited scope sets requested when obtaining access tokens, grouped by operation.
/// Each token is scoped to only what its operation needs.
/// </summary>
public static class UpvestScopes
{
    public const string UsersAdmin = "users:admin";
    public const string UsersRead = "users:read";
    public const string Checks = "checks:admin";
    public const string Accounts = "accounts:admin accounts:read";
    public const string PortfoliosAdmin = "portfolios:admin portfolios:read";
    public const string PortfoliosRead = "portfolios:read";
    public const string Payments = "payments:admin";
    public const string Valuations = "valuations:read positions:read";
}
