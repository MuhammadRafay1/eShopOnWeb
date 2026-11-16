using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.PortfolioServiceTests;

public class PortfolioServiceTests
{
    private readonly IRepository<Investor> _investors = Substitute.For<IRepository<Investor>>();
    private readonly IRepository<ModelPortfolio> _models = Substitute.For<IRepository<ModelPortfolio>>();
    private readonly IRepository<PortfolioPayment> _payments = Substitute.For<IRepository<PortfolioPayment>>();
    private readonly IRepository<Realignment> _realignments = Substitute.For<IRepository<Realignment>>();
    private readonly IUpvestPortfolioGateway _gateway = Substitute.For<IUpvestPortfolioGateway>();
    private readonly IAppLogger<PortfolioService> _logger = Substitute.For<IAppLogger<PortfolioService>>();

    private PortfolioService CreateSut() => new(_investors, _models, _payments, _realignments, _gateway, _logger);

    private static Investor ActiveInvestor(string name = "shopper@example.com")
    {
        var investor = new Investor(name);
        investor.SetUpvestUserId(Guid.NewGuid());
        investor.SetAccountGroupId(Guid.NewGuid());
        investor.SetAccountId(Guid.NewGuid());
        investor.ChooseModel("balanced", Guid.NewGuid());
        investor.SetStatus(InvestorStatus.Active);
        return investor;
    }

    [Fact]
    public async Task UpsertModel_WeightsNotHundred_Rejected()
    {
        var sut = CreateSut();
        var funds = new List<FundWeight> { new("IE00B4L5Y983", 60m), new("IE00B3RBWM25", 30m) };

        var ex = await Assert.ThrowsAsync<PortfolioRequestException>(
            () => sut.UpsertModelAsync("balanced", "Balanced", funds, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task UpsertModel_InvalidIsin_Rejected()
    {
        var sut = CreateSut();
        var funds = new List<FundWeight> { new("NOT-AN-ISIN", 100m) };

        var ex = await Assert.ThrowsAsync<PortfolioRequestException>(
            () => sut.UpsertModelAsync("m", "Model", funds, CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task PayIn_NotAnInvestor_Refused()
    {
        _investors.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Investor?)null);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<PortfolioRequestException>(
            () => sut.PayInAsync("shopper@example.com", 100m, null, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        await _gateway.DidNotReceiveWithAnyArgs().PlaceBuyOrderAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task PayIn_NoModelChosen_Refused()
    {
        var investor = new Investor("shopper@example.com");
        investor.SetUpvestUserId(Guid.NewGuid());
        investor.SetAccountGroupId(Guid.NewGuid());
        investor.SetAccountId(Guid.NewGuid());
        investor.SetStatus(InvestorStatus.Active);
        _investors.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(investor);
        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<PortfolioRequestException>(
            () => sut.PayInAsync("shopper@example.com", 100m, null, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        await _gateway.DidNotReceiveWithAnyArgs().PlaceBuyOrderAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task PayIn_ActiveInvestor_PlacesOrderAndRecordsStatus()
    {
        var investor = ActiveInvestor();
        investor.SetVirtualBankAccountIban("DE89370400440532013000");
        _investors.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(investor);
        _payments.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((PortfolioPayment?)null);
        _payments.AddAsync(Arg.Any<PortfolioPayment>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<PortfolioPayment>());
        var orderId = Guid.NewGuid();
        _gateway.PlaceBuyOrderAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), 100m, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new UpvestOrderResult(orderId, PaymentStatus.Invested));
        var sut = CreateSut();

        var payment = await sut.PayInAsync("shopper@example.com", 100m, null, CancellationToken.None);

        Assert.Equal(orderId, payment.UpvestOrderId);
        Assert.Equal(PaymentStatus.Invested, payment.Status);
        await _gateway.Received(1).CreditCashAsync("DE89370400440532013000", 100m, Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayIn_DuplicateIdempotencyKey_ReturnsExistingWithoutOrdering()
    {
        var investor = ActiveInvestor();
        _investors.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(investor);

        var key = Guid.NewGuid();
        var existing = new PortfolioPayment(key, "shopper@example.com", 100m);
        existing.SetStatus(PaymentStatus.Invested);
        _payments.GetByIdAsync(key, Arg.Any<CancellationToken>()).Returns(existing);
        var sut = CreateSut();

        var payment = await sut.PayInAsync("shopper@example.com", 100m, key, CancellationToken.None);

        Assert.Equal(key, payment.PaymentId);
        await _gateway.DidNotReceiveWithAnyArgs().PlaceBuyOrderAsync(default, default, default, default, default);
        await _gateway.DidNotReceiveWithAnyArgs().CreditCashAsync(default!, default, default!, default, default);
    }

    [Fact]
    public async Task Register_ExistingUserOnboarded_ResumesWithoutRecreatingUser()
    {
        // Investor already has a Upvest user id: onboarding must resume (not create a second user).
        var investor = new Investor("shopper@example.com");
        investor.SetUpvestUserId(Guid.NewGuid());
        _investors.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(investor);
        _gateway.CreateAccountGroupAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        _gateway.CreatePortfolioAccountAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        _gateway.GetInvestorStatusAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(InvestorStatus.Active);
        var sut = CreateSut();

        var registration = new InvestorRegistration("Ada", "Lovelace", "ada@example.com", DateTimeOffset.Parse("1985-12-10"),
            "Berlin", "DE", "DE", new RegistrationAddress("1 St", "10115", "Berlin", "DE"), "447700900123", "T1", "DE",
            new IdentityDocument("PASSPORT", "X1", "DE", DateTimeOffset.Parse("2030-01-01")));

        await sut.RegisterInvestorAsync("shopper@example.com", registration, CancellationToken.None);

        await _gateway.DidNotReceiveWithAnyArgs().CreateInvestorUserAsync(default!, default, default);
        await _gateway.Received(1).CreateAccountGroupAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPayments_RefreshesPendingFromProvider()
    {
        var orderId = Guid.NewGuid();
        var payment = new PortfolioPayment(Guid.NewGuid(), "shopper@example.com", 50m);
        payment.SetUpvestOrderId(orderId); // still Pending
        _payments.ListAsync(Arg.Any<Ardalis.Specification.ISpecification<PortfolioPayment>>(), Arg.Any<CancellationToken>())
            .Returns(new List<PortfolioPayment> { payment });
        _gateway.GetOrderStatusAsync(orderId, Arg.Any<CancellationToken>()).Returns(PaymentStatus.Invested);
        var sut = CreateSut();

        var result = await sut.GetPaymentsAsync("shopper@example.com", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(PaymentStatus.Invested, payment.Status);
        await _payments.Received(1).UpdateAsync(payment, Arg.Any<CancellationToken>());
    }
}
