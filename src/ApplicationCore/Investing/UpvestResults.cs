namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>Strongly-typed results returned by <see cref="Interfaces.IUpvestClient"/>.</summary>
public record UpvestUser(string Id, string Status);

public record UpvestAccountGroup(string Id, string Status);

public record UpvestAccount(string Id, string Status);

public record UpvestOrder(string Id, string Status);

/// <summary>An Upvest webhook-verification public key (JWKS entry): EC P-521.</summary>
public record UpvestJwk(string Kid, string X, string Y);

/// <summary>Upvest account lifecycle status values this integration cares about.</summary>
public static class UpvestStatuses
{
    // Users
    public const string UserActive = "ACTIVE";

    // Account groups / accounts
    public const string Active = "ACTIVE";

    // Orders
    public const string OrderFilled = "FILLED";
    public const string OrderCancelled = "CANCELLED";

    // Checks
    public const string CheckFailed = "FAILED";
}
