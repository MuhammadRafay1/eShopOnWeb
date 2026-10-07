using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>A shopper's enrolment, as returned to callers.</summary>
public record EnrolmentView(int EnrolmentId, EnrolmentStatus Status);

/// <summary>A shopper's balance, as returned to callers.</summary>
public record BalanceView(decimal PendingAmount, decimal InvestedAmount);

/// <summary>One of a shopper's investments, as returned to callers.</summary>
public record InvestmentView(int InvestmentId, decimal Amount, InvestmentStatus Status);

/// <summary>A shopper's investments, newest first.</summary>
public record InvestmentsView(IReadOnlyList<InvestmentView> Investments);
