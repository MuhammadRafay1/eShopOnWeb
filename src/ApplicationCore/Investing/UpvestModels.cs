namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>Result of looking up or creating an Upvest user.</summary>
public record UpvestUserResult(string Id, string Status);

/// <summary>Result of creating or looking up an Upvest account group.</summary>
public record UpvestAccountGroupResult(string Id, string Status);

/// <summary>Result of creating or looking up an Upvest account.</summary>
public record UpvestAccountResult(string Id, string Status);

/// <summary>Result of placing or looking up an Upvest order.</summary>
public record UpvestOrderResult(string Id, string Status);
