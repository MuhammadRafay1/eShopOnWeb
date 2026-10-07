using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>Outcome of creating an investor at Upvest.</summary>
public record CreateInvestorResult(Guid UpvestUserId, EnrolmentStatus Status);

/// <summary>The Upvest trading account and the account group that holds its cash.</summary>
public record UpvestAccountRef(Guid AccountId, Guid AccountGroupId);

/// <summary>Outcome of placing an investment order at Upvest.</summary>
public record PlaceInvestmentResult(Guid UpvestOrderId, InvestmentStatus Status);
