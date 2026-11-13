namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>
/// OAuth2 scopes requested per operation. Each call requests only the single scope it needs, so an
/// operation whose scope is authorised still succeeds even when neighbouring scopes are not.
/// </summary>
public static class UpvestScopes
{
    public const string UsersAdmin = "users:admin";
    public const string UsersRead = "users:read";
    public const string ChecksAdmin = "checks:admin";
    public const string TaxesAdmin = "taxes:admin";
    public const string AccountsAdmin = "accounts:admin";
    public const string AccountsRead = "accounts:read";
    public const string PortfoliosAdmin = "portfolios:admin";
    public const string PortfoliosRead = "portfolios:read";
    public const string TopupsAdmin = "topups:admin";
    public const string ValuationsRead = "valuations:read";
}
