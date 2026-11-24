namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>OAuth2 scopes requested per Upvest operation (see each endpoint's reference page).</summary>
internal static class UpvestScopes
{
    public const string UsersAdmin = "users:admin";
    public const string ChecksAdmin = "checks:admin";
    public const string TaxesAdmin = "taxes:admin";
    public const string AccountsAdmin = "accounts:admin";
    public const string AccountsRead = "accounts:read";
    public const string PortfoliosAdmin = "portfolios:admin";
    public const string PortfoliosRead = "portfolios:read";
    public const string VirtualCashAdmin = "virtual_cash_balances:admin";
    public const string PositionsRead = "positions:read";
    public const string ValuationsRead = "valuations:read";
}
