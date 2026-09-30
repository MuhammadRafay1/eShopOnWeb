using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.SavedCardAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class PaymentServiceTests
{
    private const string BuyerId = "buyer@example.com";

    private readonly IRepository<Order> _orderRepository = Substitute.For<IRepository<Order>>();
    private readonly IRepository<SavedCard> _savedCardRepository = Substitute.For<IRepository<SavedCard>>();
    private readonly IPayPalClient _payPalClient = Substitute.For<IPayPalClient>();

    private PaymentService CreateSut() => new(_orderRepository, _savedCardRepository, _payPalClient,
        Options.Create(new PayPalSettings { Currency = "USD" }));

    private static Order NewOrder() => new(BuyerId, new Address("s", "c", "st", "co", "z"),
        new List<OrderItem> { new(new CatalogItemOrdered(1, "name", "uri"), 10m, 1) });

    [Fact]
    public async Task AuthorizeAsync_AlreadyAuthorized_ReturnsExistingWithoutCallingPayPal()
    {
        var order = NewOrder();
        order.AttachAuthorization("ppo1", "auth1", "CREATED", "USD", "VISA", "1111");
        _orderRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Order>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(order);

        var sut = CreateSut();
        var result = await sut.AuthorizeAsync(1, BuyerId, null, null);

        Assert.Same(order, result);
        await _payPalClient.DidNotReceiveWithAnyArgs().AuthorizeOrderWithCardAsync(default, default!, default!, default!, default!, default!);
        await _payPalClient.DidNotReceiveWithAnyArgs().AuthorizeOrderWithVaultAsync(default, default!, default!, default!, default!, default!);
    }

    [Fact]
    public async Task AuthorizeAsync_UnknownOrder_ReturnsNull()
    {
        _orderRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Order>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns((Order?)null);

        var sut = CreateSut();
        var result = await sut.AuthorizeAsync(1, BuyerId, null, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task FulfilAsync_AlreadyFulfilled_ReturnsExistingWithoutCallingPayPalAgain()
    {
        var order = NewOrder();
        order.AttachAuthorization("ppo1", "auth1", "CREATED", "USD", "VISA", "1111");
        order.RecordCapture("cap1", "COMPLETED", 10m, 0.5m, 9.5m, DateTimeOffset.UtcNow);
        _orderRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Order>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(order);

        var sut = CreateSut();
        var result = await sut.FulfilAsync(1);

        Assert.Same(order, result);
        await _payPalClient.DidNotReceiveWithAnyArgs().CaptureAsync(default!, default, default!, default!, default!);
    }

    [Fact]
    public async Task CancelAsync_AfterFulfilment_ThrowsPaymentConflict()
    {
        var order = NewOrder();
        order.AttachAuthorization("ppo1", "auth1", "CREATED", "USD", "VISA", "1111");
        order.RecordCapture("cap1", "COMPLETED", 10m, 0.5m, 9.5m, DateTimeOffset.UtcNow);
        _orderRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Order>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(order);

        var sut = CreateSut();
        await Assert.ThrowsAsync<PaymentConflictException>(() => sut.CancelAsync(1));
    }

    [Fact]
    public async Task RefundAsync_RepeatedIdempotencyKey_ReturnsExistingWithoutCallingPayPalAgain()
    {
        var order = NewOrder();
        order.AttachAuthorization("ppo1", "auth1", "CREATED", "USD", "VISA", "1111");
        order.RecordCapture("cap1", "COMPLETED", 10m, 0.5m, 9.5m, DateTimeOffset.UtcNow);
        order.RecordRefund(new PaymentRefund("r1", 4m, "COMPLETED", "key-1"));
        _orderRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Order>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(order);

        var sut = CreateSut();
        var result = await sut.RefundAsync(1, BuyerId, 4m, "key-1");

        Assert.NotNull(result);
        Assert.Equal("r1", result!.RefundId);
        await _payPalClient.DidNotReceiveWithAnyArgs().RefundAsync(default!, default, default!, default!, default!, default!);
    }

    [Fact]
    public async Task RefundAsync_ExceedsRemainingCapturedAmount_ThrowsWithoutCallingPayPal()
    {
        var order = NewOrder();
        order.AttachAuthorization("ppo1", "auth1", "CREATED", "USD", "VISA", "1111");
        order.RecordCapture("cap1", "COMPLETED", 10m, 0.5m, 9.5m, DateTimeOffset.UtcNow);
        order.RecordRefund(new PaymentRefund("r1", 4m, "COMPLETED", "key-1"));
        _orderRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Order>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(order);

        var sut = CreateSut();
        await Assert.ThrowsAsync<RefundExceedsCapturedAmountException>(() => sut.RefundAsync(1, BuyerId, 7m, "key-2"));

        await _payPalClient.DidNotReceiveWithAnyArgs().RefundAsync(default!, default, default!, default!, default!, default!);
    }

    [Fact]
    public async Task RefundAsync_NotYetCaptured_ThrowsPaymentConflict()
    {
        var order = NewOrder();
        order.AttachAuthorization("ppo1", "auth1", "CREATED", "USD", "VISA", "1111");
        _orderRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Order>>(), Arg.Any<System.Threading.CancellationToken>())
            .Returns(order);

        var sut = CreateSut();
        await Assert.ThrowsAsync<PaymentConflictException>(() => sut.RefundAsync(1, BuyerId, null, "key-1"));
    }
}
