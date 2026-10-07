using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.InvestingServiceTests;

public class SetAsideFromPaidOrder
{
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IRepository<Investment> _investments = Substitute.For<IRepository<Investment>>();
    private readonly IUpvestInvestorGateway _gateway = Substitute.For<IUpvestInvestorGateway>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();

    private InvestingService CreateService() => new(_investors, _investments, _gateway, _logger);

    private static Investor ActiveInvestor()
    {
        var investor = new Investor("buyer-1");
        investor.SetUpvestUser(Guid.NewGuid(), Guid.NewGuid());
        investor.SetUpvestAccounts(Guid.NewGuid(), Guid.NewGuid());
        investor.SetStatus(EnrolmentStatus.Active);
        return investor;
    }

    private void GivenInvestor(Investor? investor) =>
        _investors.FirstOrDefaultAsync(Arg.Any<ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns(investor);

    [Fact]
    public async Task AcceptedInvestorSetsAsideTheRoundUp()
    {
        var investor = ActiveInvestor();
        GivenInvestor(investor);

        var setAside = await CreateService().SetAsideFromPaidOrderAsync("buyer-1", 12.30m, CancellationToken.None);

        Assert.Equal(0.70m, setAside);
        Assert.Equal(0.70m, investor.PendingAmount);
    }

    [Fact]
    public async Task WholeEuroOrderSetsAsideNothing()
    {
        var investor = ActiveInvestor();
        GivenInvestor(investor);

        var setAside = await CreateService().SetAsideFromPaidOrderAsync("buyer-1", 17.00m, CancellationToken.None);

        Assert.Equal(0m, setAside);
        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public async Task ShopperWhoIsNotAnInvestorSetsAsideNothing()
    {
        GivenInvestor(null);

        var setAside = await CreateService().SetAsideFromPaidOrderAsync("buyer-1", 12.30m, CancellationToken.None);

        Assert.Equal(0m, setAside);
    }

    [Fact]
    public async Task PendingInvestorNotYetAcceptedSetsAsideNothing()
    {
        var investor = new Investor("buyer-1");
        investor.SetUpvestUser(Guid.NewGuid(), Guid.NewGuid()); // still pending acceptance
        GivenInvestor(investor);

        var setAside = await CreateService().SetAsideFromPaidOrderAsync("buyer-1", 12.30m, CancellationToken.None);

        Assert.Equal(0m, setAside);
        Assert.Equal(0m, investor.PendingAmount);
    }

    [Fact]
    public async Task NeverThrowsWhenTheStoreFails()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns<Investor?>(_ => throw new InvalidOperationException("db down"));

        var setAside = await CreateService().SetAsideFromPaidOrderAsync("buyer-1", 12.30m, CancellationToken.None);

        Assert.Equal(0m, setAside);
    }
}
