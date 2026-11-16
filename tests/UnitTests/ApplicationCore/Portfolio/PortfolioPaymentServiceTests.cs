using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services.Portfolio;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Portfolio;

public class PortfolioPaymentServiceTests
{
    private readonly IRepository<PortfolioPayment> _payments = Substitute.For<IRepository<PortfolioPayment>>();
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IUpvestInvestmentGateway _gateway = Substitute.For<IUpvestInvestmentGateway>();
    private readonly IAppLogger<PortfolioPaymentService> _logger = Substitute.For<IAppLogger<PortfolioPaymentService>>();

    private PortfolioPaymentService Service() => new(_payments, _investors, _gateway, _logger);

    private void Investor(Investor? investor) =>
        _investors.FirstOrDefaultAsync(Arg.Any<Ardalis.Specification.ISpecification<Investor>>(), Arg.Any<CancellationToken>())
            .Returns(investor);

    [Fact]
    public async Task RefusesWhenNotAnInvestor()
    {
        Investor(null);
        await Assert.ThrowsAsync<PortfolioStateException>(() => Service().PayInAsync("shopper", 100m, CancellationToken.None));
    }

    [Fact]
    public async Task RefusesWhenInvestorPendingOrNoModelChosen()
    {
        var investor = new Investor("shopper");
        investor.SetUpvestUser(Guid.NewGuid());
        investor.SetUpvestAccount(Guid.NewGuid(), Guid.NewGuid());
        investor.SetStatus(InvestorStatus.Active); // active + account, but no model chosen
        Investor(investor);

        await Assert.ThrowsAsync<PortfolioStateException>(() => Service().PayInAsync("shopper", 100m, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsNonPositiveAmount()
    {
        await Assert.ThrowsAsync<PortfolioValidationException>(() => Service().PayInAsync("shopper", 0m, CancellationToken.None));
    }

    [Fact]
    public async Task InvestsWhenAcceptedWithModel()
    {
        var investor = new Investor("shopper");
        investor.SetUpvestUser(Guid.NewGuid());
        investor.SetUpvestAccount(Guid.NewGuid(), Guid.NewGuid());
        investor.SetStatus(InvestorStatus.Active);
        investor.ChooseModel(7);
        Investor(investor);
        _payments.AddAsync(Arg.Any<PortfolioPayment>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<PortfolioPayment>());
        _gateway.PayInAsync(Arg.Any<PayInCommand>(), Arg.Any<CancellationToken>())
            .Returns(new PayInResult(Guid.NewGuid(), Guid.NewGuid(), PaymentStatus.Invested));

        var view = await Service().PayInAsync("shopper", 100m, CancellationToken.None);

        Assert.Equal(PaymentStatus.Invested, view.Status);
        Assert.Equal(100m, view.Amount);
        await _gateway.Received(1).PayInAsync(Arg.Any<PayInCommand>(), Arg.Any<CancellationToken>());
    }
}
