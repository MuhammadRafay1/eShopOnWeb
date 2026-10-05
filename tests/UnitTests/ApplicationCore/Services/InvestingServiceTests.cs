using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class InvestingServiceTests
{
    private const string Buyer = "buyer@example.com";

    private readonly IRepository<InvestorEnrolment> _enrolments = Substitute.For<IRepository<InvestorEnrolment>>();
    private readonly IRepository<Investment> _investments = Substitute.For<IRepository<Investment>>();
    private readonly IRepository<Order> _orders = Substitute.For<IRepository<Order>>();
    private readonly IRepository<CatalogItem> _catalogItems = Substitute.For<IRepository<CatalogItem>>();
    private readonly IUriComposer _uriComposer = Substitute.For<IUriComposer>();
    private readonly IUpvestInvestingGateway _gateway = Substitute.For<IUpvestInvestingGateway>();
    private readonly IInvestingLocks _locks = Substitute.For<IInvestingLocks>();
    private readonly IAppLogger<InvestingService> _logger = Substitute.For<IAppLogger<InvestingService>>();

    private readonly InvestingService _service;

    public InvestingServiceTests()
    {
        _uriComposer.ComposePicUri(Arg.Any<string>()).Returns("pic");
        _locks.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IDisposable>(new NoopDisposable()));
        _orders.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<Order>()));
        _investments.AddAsync(Arg.Any<Investment>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<Investment>()));

        _service = new InvestingService(_enrolments, _investments, _orders, _catalogItems,
            _uriComposer, _gateway, _locks, _logger, TimeProvider.System);
    }

    [Fact]
    public async Task PlacingOrderForNonInvestorSetsNothingAside()
    {
        _enrolments.FirstOrDefaultAsync(Arg.Any<ISpecification<InvestorEnrolment>>(), Arg.Any<CancellationToken>())
            .Returns((InvestorEnrolment?)null);
        GivenCatalogItem(price: 12.30m);

        var result = await _service.PlaceOrderAsync(Buyer, new[] { new OrderLine(1, 1) }, default);

        Assert.Equal(0m, result.RoundUpAmount);
        await _orders.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _gateway.DidNotReceiveWithAnyArgs().PlaceInvestmentAsync(default, default, default, default, null!, null!, default);
    }

    [Fact]
    public async Task ActiveInvestorBelowThresholdSetsAsideButDoesNotInvest()
    {
        var enrolment = ActiveEnrolment();
        GivenEnrolment(enrolment);
        GivenCatalogItem(price: 12.30m); // round-up 0.70

        var result = await _service.PlaceOrderAsync(Buyer, new[] { new OrderLine(1, 1) }, default);

        Assert.Equal(0.70m, result.RoundUpAmount);
        Assert.Equal(0.70m, enrolment.SetAsideBalance);
        await _gateway.DidNotReceiveWithAnyArgs().PlaceInvestmentAsync(default, default, default, default, null!, null!, default);
    }

    [Fact]
    public async Task WholeEuroOrderSetsNothingAside()
    {
        var enrolment = ActiveEnrolment();
        GivenEnrolment(enrolment);
        GivenCatalogItem(price: 12.00m);

        var result = await _service.PlaceOrderAsync(Buyer, new[] { new OrderLine(1, 1) }, default);

        Assert.Equal(0m, result.RoundUpAmount);
        Assert.Equal(0m, enrolment.SetAsideBalance);
    }

    [Fact]
    public async Task CrossingThresholdInvestsWholeBalanceAndResetsIt()
    {
        var enrolment = ActiveEnrolment();
        enrolment.AddSetAside(9.80m, DateTimeOffset.UtcNow); // already set aside
        GivenEnrolment(enrolment);
        GivenCatalogItem(price: 12.30m); // round-up 0.70 -> balance 10.50 >= 10

        decimal investedAmount = 0m;
        _gateway.PlaceInvestmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Do<decimal>(a => investedAmount = a),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new UpvestInvestmentResult(Guid.NewGuid(), InvestmentStatus.Pending)));

        var result = await _service.PlaceOrderAsync(Buyer, new[] { new OrderLine(1, 1) }, default);

        Assert.Equal(0.70m, result.RoundUpAmount);
        Assert.Equal(10.50m, investedAmount);               // whole balance invested
        Assert.Equal(0m, enrolment.SetAsideBalance);        // balance starts again from zero
        await _investments.Received(1).AddAsync(Arg.Any<Investment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvestingNeverFailsTheOrderWhenUpvestThrows()
    {
        var enrolment = ActiveEnrolment();
        enrolment.AddSetAside(9.80m, DateTimeOffset.UtcNow);
        GivenEnrolment(enrolment);
        GivenCatalogItem(price: 12.30m);
        _gateway.PlaceInvestmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<UpvestInvestmentResult>(_ => throw new UpvestGatewayException("boom", 500));

        // Must not throw; the order is still placed.
        var result = await _service.PlaceOrderAsync(Buyer, new[] { new OrderLine(1, 1) }, default);

        await _orders.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        // The failed investment returns the money to the set-aside balance.
        Assert.Equal(10.50m, enrolment.SetAsideBalance);
    }

    private void GivenEnrolment(InvestorEnrolment enrolment) =>
        _enrolments.FirstOrDefaultAsync(Arg.Any<ISpecification<InvestorEnrolment>>(), Arg.Any<CancellationToken>())
            .Returns(enrolment);

    private void GivenCatalogItem(decimal price)
    {
        var item = new CatalogItem(1, 1, "desc", "name", price, "pic.png");
        // Id has a protected setter (set by EF); assign it for the test so the order-item snapshot is valid.
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(item, 1);
        _catalogItems.ListAsync(Arg.Any<ISpecification<CatalogItem>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CatalogItem> { item });
    }

    private static InvestorEnrolment ActiveEnrolment()
    {
        var now = DateTimeOffset.UtcNow;
        var e = new InvestorEnrolment(Buyer, "e@e.de", "TIN", "DE", now);
        e.RecordUpvestUser(Guid.NewGuid(), now);
        e.RecordUpvestAccountGroup(Guid.NewGuid(), now);
        e.RecordUpvestAccount(Guid.NewGuid(), now);
        e.SetStatus(EnrolmentStatus.Active, now);
        return e;
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
