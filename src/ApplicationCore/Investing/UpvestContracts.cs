using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>The Upvest user created for a shopper, and its current acceptance status.</summary>
public record UpvestUserRef(string UserId, EnrolmentStatus Status);

/// <summary>The Upvest account group and trading account provisioned for a shopper.</summary>
public record UpvestAccountRef(string AccountGroupId, string AccountId);

/// <summary>The outcome of placing an investment order at Upvest.</summary>
public record UpvestInvestmentRef(string OrderId, InvestmentStatus Status);
