using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;

namespace Microsoft.eShopWeb.ApplicationCore.Investing;

/// <summary>A line of a placed order: a catalog item and how many of it.</summary>
public record OrderLine(int CatalogItemId, int Quantity);

/// <summary>Result of placing an order: the new order's id and the change it set aside (0 if none).</summary>
public record OrderPlacementResult(int OrderId, decimal RoundUpAmount);

/// <summary>A shopper's enrolment state.</summary>
public record EnrolmentView(Guid EnrolmentId, EnrolmentStatus Status);

/// <summary>One of a shopper's investments.</summary>
public record InvestmentView(Guid InvestmentId, decimal Amount, InvestmentStatus Status, DateTimeOffset CreatedAt);

/// <summary>A shopper's current set-aside and total-invested amounts.</summary>
public record BalanceView(decimal PendingAmount, decimal InvestedAmount);
