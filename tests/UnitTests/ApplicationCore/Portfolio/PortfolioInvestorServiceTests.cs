using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Portfolio;

public class PortfolioInvestorServiceTests
{
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IUpvestInvestmentGateway _gateway = Substitute.For<IUpvestInvestmentGateway>();
    private readonly IAppLogger<PortfolioInvestorService> _logger = Substitute.For<IAppLogger<PortfolioInvestorService>>();

    private PortfolioInvestorService Service() => new(_investors, _gateway, _logger);

    private static InvestorSignup Signup() => new(
        "Erika", "Mustermann", "erika@example.com", new DateOnly(1988, 4, 12),
        "Berlin", "DE", "DE",
        new InvestorAddress("Musterstrasse 1", "10115", "Berlin", "DE"),
        "491701234567", "12345678901", "DE",
        new InvestorIdentityDocument("NATIONAL_ID", "T220001293", "DE", new DateOnly(2031, 6, 30)));

    [Fact]
    public async Task SignUpCreatesClaimAndReturnsProvisionedView()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);
        _investors.AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<Investor>());
        var userId = Guid.NewGuid();
        _gateway.CreateInvestorAsync(Arg.Any<InvestorSignup>(), Arg.Any<InvestorProvisioningKeys>(), Arg.Any<CancellationToken>())
            .Returns(new InvestorProvisioning(userId, null, null, InvestorStatus.Pending));

        var view = await Service().SignUpAsync("shopper", Signup(), CancellationToken.None);

        Assert.Equal(userId, view.InvestorId);
        Assert.Equal(InvestorStatus.Pending, view.Status);
        await _investors.Received(1).AddAsync(Arg.Any<Investor>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatusReturnsNullWhenNotAnInvestor()
    {
        _investors.FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns((Investor?)null);

        Assert.Null(await Service().GetStatusAsync("shopper", CancellationToken.None));
    }
}
