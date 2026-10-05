using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class InvestingServiceHandleOrderPaidTests
{
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IRepository<Investment> _investments = Substitute.For<IRepository<Investment>>();
    private readonly IUpvestGateway _gateway = Substitute.For<IUpvestGateway>();
    private readonly IEnrolmentReconciler _reconciler = Substitute.For<IEnrolmentReconciler>();
    private readonly IInvestmentQueue _queue = Substitute.For<IInvestmentQueue>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();

    private InvestingService CreateService() =>
        new(_investors, _investments, _gateway, _reconciler, _queue, _logger);

    private static Investor ActiveInvestor(string buyerId, long pendingCents)
    {
        var investor = new Investor(buyerId);
        investor.LinkUpvestUser("user-1");
        investor.MarkActive();
        investor.LinkAccount("group-1", "account-1");
        investor.MarkAccountActive();
        if (pendingCents > 0)
        {
            investor.AddSetAside(pendingCents);
        }
        return investor;
    }

    [Fact]
    public async Task Not_enrolled_shopper_sets_aside_nothing()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);

        var result = await CreateService().HandleOrderPaidAsync("unknown-buyer", 12.30m, CancellationToken.None);

        Assert.Equal(0, result);
        _queue.DidNotReceive().Enqueue(Arg.Any<int>());
    }

    [Fact]
    public async Task Active_shopper_below_threshold_sets_aside_without_investing()
    {
        var investor = ActiveInvestor("buyer-below", 0);
        _investors.FirstOrDefaultAsync(Arg.Any<ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns(investor);

        var result = await CreateService().HandleOrderPaidAsync("buyer-below", 12.30m, CancellationToken.None); // €0.70

        Assert.Equal(70, result);
        Assert.Equal(70, investor.PendingAmountCents);
        _queue.DidNotReceive().Enqueue(Arg.Any<int>());
    }

    [Fact]
    public async Task Active_shopper_crossing_threshold_starts_one_investment()
    {
        var investor = ActiveInvestor("buyer-cross", 950); // €9.50 already set aside
        _investors.FirstOrDefaultAsync(Arg.Any<ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns(investor);

        var result = await CreateService().HandleOrderPaidAsync("buyer-cross", 8.50m, CancellationToken.None); // €0.50 → €10.00

        Assert.Equal(50, result);
        await _investments.Received(1).AddAsync(Arg.Is<Investment>(i => i.AmountCents == 1000), Arg.Any<CancellationToken>());
        _queue.Received(1).Enqueue(Arg.Any<int>());
        Assert.Equal(0, investor.PendingAmountCents); // whole balance moved into the investment
    }
}
