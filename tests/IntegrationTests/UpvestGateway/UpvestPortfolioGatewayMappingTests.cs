using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using UpvestInvestmentApi.Models;
using UpvestInvestmentApi.Models.Enums;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.UpvestGateway;

/// <summary>
/// Pure-mapping tests for the Upvest gateway: order-status mapping, realignment-status aggregation, and the
/// amount/weight string formatting that must satisfy Upvest's wire regexes.
/// </summary>
public class UpvestPortfolioGatewayMappingTests
{
    [Theory]
    [InlineData("FILLED", PaymentStatus.Invested)]
    [InlineData("SETTLED", PaymentStatus.Invested)]
    [InlineData("CANCELLED", PaymentStatus.Failed)]
    [InlineData("NEW", PaymentStatus.Pending)]
    [InlineData("PROCESSING", PaymentStatus.Pending)]
    public void MapOrderStatus_MapsEachUpvestStatus(string wire, PaymentStatus expected)
    {
        Status65.TryGetKnownValue(wire, out var status);
        Assert.Equal(expected, UpvestPortfolioGateway.MapOrderStatus(status!));
    }

    [Fact]
    public void AggregateRealignmentStatus_AllFilled_Completed()
    {
        var orders = new List<PortfoliosRebalancingExecutionOrder> { Order(Status71.Filled), Order(Status71.Filled) };
        Assert.Equal(RealignmentStatus.Completed, UpvestPortfolioGateway.AggregateRealignmentStatus(orders));
    }

    [Fact]
    public void AggregateRealignmentStatus_AnyProcessing_Pending()
    {
        var orders = new List<PortfoliosRebalancingExecutionOrder> { Order(Status71.Filled), Order(Status71.Processing) };
        Assert.Equal(RealignmentStatus.Pending, UpvestPortfolioGateway.AggregateRealignmentStatus(orders));
    }

    [Fact]
    public void AggregateRealignmentStatus_RealCancellation_Failed()
    {
        var orders = new List<PortfoliosRebalancingExecutionOrder> { Order(Status71.Filled), Order(Status71.Cancelled, "ACCOUNT_IS_EMPTY") };
        Assert.Equal(RealignmentStatus.Failed, UpvestPortfolioGateway.AggregateRealignmentStatus(orders));
    }

    [Fact]
    public void AggregateRealignmentStatus_BenignCancellation_Completed()
    {
        // CANCELLED because the portfolio was already balanced is a success, not a failure.
        var orders = new List<PortfoliosRebalancingExecutionOrder> { Order(Status71.Filled), Order(Status71.Cancelled, "PORTFOLIO_IS_BALANCED") };
        Assert.Equal(RealignmentStatus.Completed, UpvestPortfolioGateway.AggregateRealignmentStatus(orders));
    }

    [Fact]
    public void AggregateRealignmentStatus_NoOrders_Pending()
    {
        Assert.Equal(RealignmentStatus.Pending, UpvestPortfolioGateway.AggregateRealignmentStatus(new List<PortfoliosRebalancingExecutionOrder>()));
    }

    [Theory]
    [InlineData(100, "100")]
    [InlineData(60, "60")]
    [InlineData(33.33333, "33.33333")]
    public void FormatWeight_MatchesUpvestRegex(double weight, string expected)
    {
        Assert.Equal(expected, UpvestPortfolioGateway.FormatWeight((decimal)weight));
    }

    [Theory]
    [InlineData(100, "100.00")]
    [InlineData(100.5, "100.50")]
    [InlineData(99.99, "99.99")]
    public void FormatAmount_AlwaysTwoDecimals(double amount, string expected)
    {
        Assert.Equal(expected, UpvestPortfolioGateway.FormatAmount((decimal)amount));
    }

    [Fact]
    public void ToClientReference_StripsHyphensAndFitsLimit()
    {
        var guid = Guid.NewGuid();
        var reference = UpvestPortfolioGateway.ToClientReference(guid.ToString());
        Assert.DoesNotContain("-", reference);
        Assert.True(reference.Length <= 35);
    }

    private static PortfoliosRebalancingExecutionOrder Order(Status71 status, string? cancellationReason = null)
    {
        CancellationReason3? reason = null;
        if (cancellationReason is not null && CancellationReason3.TryGetKnownValue(cancellationReason, out var known))
        {
            reason = known;
        }

        return new PortfoliosRebalancingExecutionOrder
        {
            Id = Guid.NewGuid(),
            ExecutionId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            PortfolioOrderId = Guid.NewGuid(),
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CancellationReason = reason
        };
    }
}
