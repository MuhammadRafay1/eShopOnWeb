using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>A requested order line: a catalog item and a quantity.</summary>
public record OrderLine(int CatalogItemId, int Quantity);

/// <summary>The outcome of an enrolment request/query.</summary>
public record EnrolmentResult(Guid EnrolmentId, EnrolmentStatus Status);

/// <summary>The outcome of placing an order: the order id and what it set aside (in cents).</summary>
public record PlaceOrderResult(int OrderId, long RoundUpCents);

/// <summary>A single investment, for the investments list.</summary>
public record InvestmentView(Guid InvestmentId, long AmountCents, InvestmentStatus Status);

/// <summary>The shopper's spare-change balance: set aside (pending) and invested, in cents.</summary>
public record BalanceResult(long PendingCents, long InvestedCents);
