namespace Microsoft.eShopWeb.Infrastructure.Portfolios;

/// <summary>
/// The least-privilege OAuth2 scope each Upvest operation requires. Tokens are requested per-scope so
/// that an operation the environment is not authorised for fails on its own, without blocking the rest.
/// </summary>
internal static class UpvestScopes
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
