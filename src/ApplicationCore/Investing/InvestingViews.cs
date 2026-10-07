using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>A shopper's enrolment, as shown to them.</summary>
public record EnrolmentView(Guid EnrolmentId, EnrolmentStatus Status);

/// <summary>One investment made on a shopper's behalf, as shown to them.</summary>
public record InvestmentView(Guid InvestmentId, decimal Amount, InvestmentStatus Status);

/// <summary>A shopper's running balance.</summary>
public record BalanceView(decimal PendingAmount, decimal InvestedAmount);
