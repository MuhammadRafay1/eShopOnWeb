using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Services.PayPal;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure;

/// <summary>
/// State-machine tests for <see cref="PaymentService"/> with a mocked <see cref="IPayPalClient"/>.
/// These cover the branches that can't be exercised against the live sandbox on demand —
/// notably the stale-hold reauthorization path — plus the idempotency short-circuits.
/// </summary>
public class PaymentServiceTests
{
    private readonly IRepository<Order> _orders = Substitute.For<IRepository<Order>>();
    private readonly IRepository<Payment> _payments = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<PaymentMethod> _methods = Substitute.For<IRepository<PaymentMethod>>();
    private readonly IRepository<CatalogItem> _catalog = Substitute.For<IRepository<CatalogItem>>();
    private readonly IPayPalClient _paypal = Substitute.For<IPayPalClient>();
    private readonly IUriComposer _uri = Substitute.For<IUriComposer>();
    private readonly IAppLogger<PaymentService> _logger = Substitute.For<IAppLogger<PaymentService>>();

    private const string Buyer = "buyer@example.com";

    private PaymentService NewService() =>
        new(_orders, _payments, _methods, _catalog, _paypal, _uri,
            Options.Create(new PayPalOptions { Currency = "USD", Environment = "sandbox" }), _logger);

    private static Order OrderWithStatus(OrderStatus status)
    {
        var order = new Order(Buyer, new Address("1 St", "City", "ST", "US", "00000"),
            new List<OrderItem> { new(new CatalogItemOrdered(1, "Item", "pic.png"), 50m, 1) });
        if (status >= OrderStatus.PaymentAuthorized) order.MarkPaymentAuthorized();
        if (status >= OrderStatus.Fulfilled && status != OrderStatus.Cancelled) order.MarkFulfilled();
        return order;
    }

    [Fact]
    public async Task Fulfil_WhenAuthorizationStale_ReauthorizesThenCaptures()
    {
        var order = OrderWithStatus(OrderStatus.PaymentAuthorized);
        var payment = new Payment(1, 50m, "USD");
        payment.SetAuthorization("PPO", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(-1)); // expired

        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);
        _paypal.GetAuthorizationAsync("AUTH1", Arg.Any<CancellationToken>())
            .Returns(new PayPalAuthorizationDetails { Id = "AUTH1", Status = "CREATED", ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) });
        _paypal.ReauthorizeAsync("AUTH1", 50m, "USD", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalAuthorizationResult { AuthorizationId = "AUTH2", Status = "CREATED", ExpiresAt = DateTimeOffset.UtcNow.AddDays(3) });
        _paypal.CaptureAsync("AUTH2", 50m, "USD", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalCaptureResult { CaptureId = "CAP1", Status = "COMPLETED", GrossAmount = 50m, PayPalFee = 2m, NetAmount = 48m });

        var result = await NewService().FulfilOrderAsync(1, CancellationToken.None);

        Assert.True(result.Reauthorized);
        Assert.Equal("CAP1", result.CaptureId);
        Assert.Equal("COMPLETED", result.CaptureStatus);
        Assert.Equal(OrderStatus.Fulfilled.ToString(), result.OrderStatus);
        await _paypal.Received(1).ReauthorizeAsync("AUTH1", 50m, "USD", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fulfil_WhenReauthorizationFails_ThrowsOperatorActionable422()
    {
        var order = OrderWithStatus(OrderStatus.PaymentAuthorized);
        var payment = new Payment(1, 50m, "USD");
        payment.SetAuthorization("PPO", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(-1));

        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);
        _paypal.GetAuthorizationAsync("AUTH1", Arg.Any<CancellationToken>())
            .Returns(new PayPalAuthorizationDetails { Id = "AUTH1", Status = "CREATED", ExpiresAt = DateTimeOffset.UtcNow.AddDays(-40) });
        _paypal.ReauthorizeAsync("AUTH1", 50m, "USD", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PayPalApiException(422, "UNPROCESSABLE_ENTITY", "cannot reauthorize", "dbg", null));

        await Assert.ThrowsAsync<UnprocessableEntityException>(() => NewService().FulfilOrderAsync(1, CancellationToken.None));
        await _paypal.DidNotReceive().CaptureAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fulfil_WhenAuthorizationFresh_CapturesWithoutReauthorization()
    {
        var order = OrderWithStatus(OrderStatus.PaymentAuthorized);
        var payment = new Payment(1, 50m, "USD");
        payment.SetAuthorization("PPO", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(2));

        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);
        _paypal.GetAuthorizationAsync("AUTH1", Arg.Any<CancellationToken>())
            .Returns(new PayPalAuthorizationDetails { Id = "AUTH1", Status = "CREATED", ExpiresAt = DateTimeOffset.UtcNow.AddDays(2) });
        _paypal.CaptureAsync("AUTH1", 50m, "USD", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalCaptureResult { CaptureId = "CAP1", Status = "COMPLETED", GrossAmount = 50m, PayPalFee = 2m, NetAmount = 48m });

        var result = await NewService().FulfilOrderAsync(1, CancellationToken.None);

        Assert.False(result.Reauthorized);
        await _paypal.DidNotReceive().ReauthorizeAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pay_WhenAlreadyAuthorized_ReturnsExistingWithoutCallingPayPal()
    {
        var order = OrderWithStatus(OrderStatus.PaymentAuthorized);
        var payment = new Payment(1, 50m, "USD");
        payment.SetAuthorization("PPO", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(3));

        _orders.FirstOrDefaultAsync(Arg.Any<OrderWithItemsByIdSpecification>(), Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);

        var card = new CardInput("4111111111111111", 1, 2030, "123", "Test", null);
        var result = await NewService().PayOrderAsync(Buyer, 1, card, null, CancellationToken.None);

        Assert.True(result.AlreadyAuthorized);
        Assert.Equal("AUTH1", result.AuthorizationId);
        await _paypal.DidNotReceive().AuthorizeOrderAsync(Arg.Any<PayPalAuthorizeOrderRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pay_WithBothCardAndMethod_ThrowsBadRequest()
    {
        var card = new CardInput("4111111111111111", 1, 2030, "123", "Test", null);
        await Assert.ThrowsAsync<BadRequestException>(() =>
            NewService().PayOrderAsync(Buyer, 1, card, 5, CancellationToken.None));
    }

    [Fact]
    public async Task Pay_ForeignOrder_ThrowsNotFound()
    {
        var order = OrderWithStatus(OrderStatus.AwaitingPayment); // owned by Buyer
        _orders.FirstOrDefaultAsync(Arg.Any<OrderWithItemsByIdSpecification>(), Arg.Any<CancellationToken>()).Returns(order);

        var card = new CardInput("4111111111111111", 1, 2030, "123", "Test", null);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            NewService().PayOrderAsync("someone-else@example.com", 1, card, null, CancellationToken.None));
    }

    [Fact]
    public async Task Refund_RepeatIdempotencyKey_ReturnsExistingWithoutCallingPayPal()
    {
        var order = OrderWithStatus(OrderStatus.Fulfilled);
        var payment = new Payment(1, 50m, "USD");
        payment.SetAuthorization("PPO", "AUTH1", "CREATED", null);
        payment.SetCapture("CAP1", "COMPLETED", 50m, 2m, 48m);
        payment.AddRefund("R1", 20m, "COMPLETED", "dup-key");

        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);

        var result = await NewService().RefundOrderAsync(Buyer, 1, 20m, "dup-key", CancellationToken.None);

        Assert.True(result.AlreadyProcessed);
        Assert.Equal("R1", result.RefundId);
        await _paypal.DidNotReceive().RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refund_ExceedingRemainder_ThrowsUnprocessable()
    {
        var order = OrderWithStatus(OrderStatus.Fulfilled);
        var payment = new Payment(1, 50m, "USD");
        payment.SetAuthorization("PPO", "AUTH1", "CREATED", null);
        payment.SetCapture("CAP1", "COMPLETED", 50m, 2m, 48m);
        payment.AddRefund("R1", 40m, "COMPLETED", "k1"); // 10 remaining

        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);

        await Assert.ThrowsAsync<UnprocessableEntityException>(() =>
            NewService().RefundOrderAsync(Buyer, 1, 20m, "k2", CancellationToken.None));
        await _paypal.DidNotReceive().RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_AuthorizedOrder_VoidsHold()
    {
        var order = OrderWithStatus(OrderStatus.PaymentAuthorized);
        var payment = new Payment(1, 50m, "USD");
        payment.SetAuthorization("PPO", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(3));

        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);

        var result = await NewService().CancelOrderAsync(1, CancellationToken.None);

        Assert.True(result.AuthorizationVoided);
        Assert.Equal(OrderStatus.Cancelled.ToString(), result.OrderStatus);
        await _paypal.Received(1).VoidAuthorizationAsync("AUTH1", Arg.Any<CancellationToken>());
    }
}
