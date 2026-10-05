using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>The outcome of placing an investment order at Upvest.</summary>
public record UpvestInvestmentPlacement(Guid OrderId, InvestmentStatus Outcome);
