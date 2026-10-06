using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Investing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.Investing;

/// <summary>
/// Exercises the investing orchestration against the real in-memory store with a faked provider gateway —
/// so the round-up ledger, threshold-crossing investment, balances and views are verified without the
/// live Upvest sandbox.
/// </summary>
public class InvestingServiceTests
{
    private static (InvestingService service, IUpvestInvestorGateway gateway) Build()
    {
        var options = new DbContextOptionsBuilder<CatalogContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var ctx = new CatalogContext(options);
        var investors = new EfRepository<Investor>(ctx);
        var investments = new EfRepository<Investment>(ctx);
        var gateway = Substitute.For<IUpvestInvestorGateway>();
        var service = new InvestingService(investors, investments, gateway, NullLogger<InvestingService>.Instance);
        return (service, gateway);
    }

    private static InvestorEnrolmentDetails SampleDetails() => new(
        "Ada", "Lovelace", "ada@example.com", new DateOnly(1990, 1, 1), "DE",
        "1 Main St", "10115", "Berlin", "DE", "491234567", "TAX123", "DE");

    [Theory]
    [InlineData(12.30, 70)]
    [InlineData(12.00, 0)]
    [InlineData(12.99, 1)]
    [InlineData(0.01, 99)]
    [InlineData(10.00, 0)]
    [InlineData(7.55, 45)]
    public void RoundUpCents_rounds_to_next_euro(double total, long expected) =>
        Assert.Equal(expected, InvestingService.RoundUpCents((decimal)total));

    [Fact]
    public async Task Not_enrolled_sets_nothing_aside()
    {
        var (service, _) = Build();
        var roundUp = await service.OnOrderPaidAsync("buyer-x", 12.30m, CancellationToken.None);
        Assert.Equal(0, roundUp);
    }

    [Fact]
    public async Task Enrolment_is_idempotent_and_reflects_acceptance()
    {
        var (service, gateway) = Build();
        gateway.ProvisionInvestorAsync("buyer-1", Arg.Any<InvestorEnrolmentDetails>(), Arg.Any<CancellationToken>())
            .Returns(new InvestorProvisionResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), EnrolmentStatus.Active));

        var first = await service.EnrolAsync("buyer-1", SampleDetails(), CancellationToken.None);
        var second = await service.EnrolAsync("buyer-1", SampleDetails(), CancellationToken.None);

        Assert.Equal(EnrolmentStatus.Active, first.Status);
        Assert.Equal(first.EnrolmentId, second.EnrolmentId);
        // Provider provisioning happens once, not on the idempotent repeat.
        await gateway.Received(1).ProvisionInvestorAsync("buyer-1", Arg.Any<InvestorEnrolmentDetails>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crossing_ten_euros_invests_the_whole_balance_and_resets()
    {
        var (service, gateway) = Build();
        var accountId = Guid.NewGuid();
        gateway.ProvisionInvestorAsync("buyer-2", Arg.Any<InvestorEnrolmentDetails>(), Arg.Any<CancellationToken>())
            .Returns(new InvestorProvisionResult(Guid.NewGuid(), Guid.NewGuid(), accountId, EnrolmentStatus.Active));
        gateway.PlaceInvestmentOrderAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new InvestmentOrderResult(Guid.NewGuid(), InvestmentStatus.Pending));

        await service.EnrolAsync("buyer-2", SampleDetails(), CancellationToken.None);

        // 10 orders of €0.01 each set aside 99c → 990c (below threshold, no investment yet).
        for (var i = 0; i < 10; i++)
            Assert.Equal(99, await service.OnOrderPaidAsync("buyer-2", 0.01m, CancellationToken.None));

        var beforeCross = await service.GetBalanceAsync("buyer-2", CancellationToken.None);
        Assert.Equal(990, beforeCross.PendingAmountCents);
        Assert.Equal(0, beforeCross.InvestedAmountCents);
        await gateway.DidNotReceive().PlaceInvestmentOrderAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        // 11th order crosses €10: whole 1089c is invested and the ledger resets to 0.
        Assert.Equal(99, await service.OnOrderPaidAsync("buyer-2", 0.01m, CancellationToken.None));

        await gateway.Received(1).PlaceInvestmentOrderAsync(
            Arg.Any<Guid>(), accountId, 1089, Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        var after = await service.GetBalanceAsync("buyer-2", CancellationToken.None);
        Assert.Equal(0, after.PendingAmountCents);
        Assert.Equal(1089, after.InvestedAmountCents);

        var investments = await service.GetInvestmentsAsync("buyer-2", CancellationToken.None);
        Assert.Single(investments);
        Assert.Equal(1089, investments[0].AmountCents);
        Assert.Equal(InvestmentStatus.Pending, investments[0].Status);
    }

    [Fact]
    public async Task Investing_failure_never_fails_the_order()
    {
        var (service, gateway) = Build();
        gateway.ProvisionInvestorAsync("buyer-3", Arg.Any<InvestorEnrolmentDetails>(), Arg.Any<CancellationToken>())
            .Returns(new InvestorProvisionResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), EnrolmentStatus.Active));
        gateway.PlaceInvestmentOrderAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<InvestmentOrderResult>>(_ => throw new UpvestProviderException("unreachable") { OutcomeUnknown = true });

        await service.EnrolAsync("buyer-3", SampleDetails(), CancellationToken.None);

        // Set aside 1000c across ~11 orders; the invest attempt throws but the round-up is still returned.
        long last = 0;
        for (var i = 0; i < 11; i++)
            last = await service.OnOrderPaidAsync("buyer-3", 0.01m, CancellationToken.None);

        Assert.Equal(99, last); // the order's set-aside is reported despite the provider failure

        // The investment is left pending for reconciliation; the money is accounted for (not lost).
        var investments = await service.GetInvestmentsAsync("buyer-3", CancellationToken.None);
        Assert.Single(investments);
        Assert.Equal(InvestmentStatus.Pending, investments[0].Status);
    }

    [Fact]
    public async Task Definite_rejection_refunds_the_ledger()
    {
        var (service, gateway) = Build();
        gateway.ProvisionInvestorAsync("buyer-4", Arg.Any<InvestorEnrolmentDetails>(), Arg.Any<CancellationToken>())
            .Returns(new InvestorProvisionResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), EnrolmentStatus.Active));
        gateway.PlaceInvestmentOrderAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task<InvestmentOrderResult>>(_ => throw new UpvestProviderException("rejected", System.Net.HttpStatusCode.UnprocessableEntity));

        await service.EnrolAsync("buyer-4", SampleDetails(), CancellationToken.None);
        for (var i = 0; i < 11; i++)
            await service.OnOrderPaidAsync("buyer-4", 0.01m, CancellationToken.None);

        // Rejected order is rolled back: no investment recorded and the balance accrues again.
        var investments = await service.GetInvestmentsAsync("buyer-4", CancellationToken.None);
        Assert.Empty(investments);
        var balance = await service.GetBalanceAsync("buyer-4", CancellationToken.None);
        Assert.Equal(1089, balance.PendingAmountCents);
        Assert.Equal(0, balance.InvestedAmountCents);
    }
}
