using Microsoft.eShopWeb.ApplicationCore;
using System;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.UnitTests.Builders;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class FulfilAsyncTests
{
    private readonly IRepository<Order> _orderRepo = Substitute.For<IRepository<Order>>();
    private readonly IRepository<Payment> _paymentRepo = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<CatalogItem> _catalogItemRepo = Substitute.For<IRepository<CatalogItem>>();
    private readonly IRepository<SavedPaymentMethod> _savedPaymentMethodRepo = Substitute.For<IRepository<SavedPaymentMethod>>();
    private readonly IUriComposer _uriComposer = Substitute.For<IUriComposer>();
    private readonly IPayPalClient _payPalClient = Substitute.For<IPayPalClient>();

    private PaymentService BuildService() => new(
        _orderRepo, _paymentRepo, _catalogItemRepo, _savedPaymentMethodRepo, _uriComposer, _payPalClient,
        new PayPalSettings { ClientId = "id", ClientSecret = "secret", Environment = "sandbox", Currency = "USD" });

    private (Order Order, Payment Payment) AuthorizedOrderAndPayment(DateTimeOffset? expiresAt = null)
    {
        var order = new OrderBuilder().WithDefaultValues();
        order.MarkAuthorized();

        var payment = new Payment(1, order.BuyerId, "USD", order.Total());
        payment.RecordAuthorization("ppOrder", "auth1", "CREATED", expiresAt, "VISA", "1111");

        _orderRepo.FirstOrDefaultAsync(Arg.Any<OrderWithItemsByIdSpec>(), default).Returns(order);
        _paymentRepo.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), default).Returns(payment);

        return (order, payment);
    }

    [Fact]
    public async Task SecondFulfilCall_ReturnsExistingCapture_WithoutCapturingAgain()
    {
        var (order, payment) = AuthorizedOrderAndPayment();
        payment.RecordCapture("capture1", "COMPLETED", order.Total(), 1m, order.Total() - 1m);

        var service = BuildService();
        var result = await service.FulfilAsync(1);

        Assert.Same(payment, result);
        await _payPalClient.DidNotReceive().CaptureAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task StaleAuthorization_IsRenewedThenCaptured()
    {
        var (order, payment) = AuthorizedOrderAndPayment(expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        _payPalClient.ReauthorizeAsync("auth1", order.Total(), "USD", Arg.Any<string>())
            .Returns(new ReauthorizeResult("auth2", "CREATED", DateTimeOffset.UtcNow.AddDays(3)));
        _payPalClient.CaptureAsync("auth2", order.Total(), "USD", Arg.Any<string>())
            .Returns(new CaptureResult("capture1", "COMPLETED", order.Total(), 1m, order.Total() - 1m));

        var service = BuildService();
        var result = await service.FulfilAsync(1);

        Assert.Equal("capture1", result.CaptureId);
        Assert.Equal("auth2", result.AuthorizationId);
        await _payPalClient.Received(1).ReauthorizeAsync("auth1", order.Total(), "USD", Arg.Any<string>());
        await _payPalClient.Received(1).CaptureAsync("auth2", order.Total(), "USD", Arg.Any<string>());
    }

    [Fact]
    public async Task AuthorizationThatCanNoLongerBeRenewed_SurfacesAnOperatorActionableError()
    {
        var (order, _) = AuthorizedOrderAndPayment(expiresAt: DateTimeOffset.UtcNow.AddDays(-40));

        _payPalClient.ReauthorizeAsync("auth1", order.Total(), "USD", Arg.Any<string>())
            .Returns<Task<ReauthorizeResult>>(_ => throw new PayPalApiException("30 days have passed", "UNPROCESSABLE_ENTITY", "debug-123", 422));

        var service = BuildService();

        var ex = await Assert.ThrowsAsync<AuthorizationNotRenewableException>(() => service.FulfilAsync(1));

        Assert.Contains("collect payment again", ex.Message);
        Assert.Equal("debug-123", ex.DebugId);
        await _payPalClient.DidNotReceive().CaptureAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task FulfilBeforeAuthorized_Throws()
    {
        var order = new OrderBuilder().WithDefaultValues();
        _orderRepo.FirstOrDefaultAsync(Arg.Any<OrderWithItemsByIdSpec>(), default).Returns(order);
        _paymentRepo.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), default).Returns((Payment?)null);

        var service = BuildService();

        await Assert.ThrowsAsync<InvalidOrderStateException>(() => service.FulfilAsync(1));
    }
}
