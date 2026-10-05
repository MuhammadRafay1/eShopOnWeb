namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>A user (investor) resource at Upvest. Status e.g. INACTIVE, ACTIVE.</summary>
public sealed record UpvestUserRef(string Id, string Status);

/// <summary>An account group resource at Upvest. Status e.g. PENDING_APPROVAL, ACTIVE.</summary>
public sealed record UpvestAccountGroupRef(string Id, string Status);

/// <summary>A trading account resource at Upvest. Status e.g. PENDING_APPROVAL, ACTIVE.</summary>
public sealed record UpvestAccountRef(string Id, string Status);

/// <summary>An order resource at Upvest. Status e.g. NEW, PROCESSING, FILLED, CANCELLED.</summary>
public sealed record UpvestOrderRef(string Id, string Status);
