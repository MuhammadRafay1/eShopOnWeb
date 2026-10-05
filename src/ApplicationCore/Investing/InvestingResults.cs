using System;
using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>Outcome of opting in, or a snapshot of where an enrolment has got to.</summary>
public record EnrolmentResult(Guid EnrolmentId, string Status);

/// <summary>A shopper's set-aside and invested totals.</summary>
public record BalanceResult(decimal PendingAmount, decimal InvestedAmount);

/// <summary>A single investment as shown to the shopper.</summary>
public record InvestmentResult(Guid InvestmentId, decimal Amount, string Status);

/// <summary>Result of applying a paid order to a shopper's set-aside ledger.</summary>
public record SetAsideResult(decimal RoundUpAmount);
