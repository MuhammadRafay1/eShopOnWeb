namespace Microsoft.eShopWeb.Infrastructure.Upvest;

/// <summary>Shared constants for talking to Upvest.</summary>
public static class UpvestHttp
{
    public const string ClientName = "upvest";

    public static class Scopes
    {
        public const string UsersRead = "users:read";
        public const string UsersAdmin = "users:admin";
        public const string TaxesAdmin = "taxes:admin";
        public const string ChecksAdmin = "checks:admin";
        public const string AccountsRead = "accounts:read";
        public const string AccountsAdmin = "accounts:admin";
        public const string PortfoliosRead = "portfolios:read";
        public const string PortfoliosAdmin = "portfolios:admin";
        public const string TestsAdmin = "tests:admin";
        public const string ValuationsRead = "valuations:read";
    }

    public static class Sandbox
    {
        /// <summary>Upvest's sandbox pooling account IBAN used to simulate inbound credit transfers.</summary>
        public const string PoolIban = "NL70ABNA0106295004";

        /// <summary>Placeholder counterparty details; ignored by Upvest's matching, present only to pass validation.</summary>
        public const string CounterpartyIban = "DE95500105173934555844";
        public const string CounterpartyBic = "DEUTDEFF";
    }
}
