using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>Progress of a shopper's onboarding at Upvest, as the account group/account come into being.</summary>
public record OnboardingProgress(
    Guid? AccountGroupId,
    Guid? AccountId,
    EnrolmentStatus Status);

/// <summary>The outcome of placing an investment order at Upvest.</summary>
public record UpvestInvestmentResult(
    Guid OrderId,
    InvestmentStatus Status);
