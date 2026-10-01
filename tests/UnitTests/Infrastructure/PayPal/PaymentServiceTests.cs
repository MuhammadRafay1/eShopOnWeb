using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.Infrastructure.PayPal;

public class PaymentServiceTests
{
    private const string Buyer = "shopperA@example.com";

    private static CatalogContext NewContext() =>
        new(new DbContextOptionsBuilder<CatalogContext>()
            .UseInMemoryDatabase($"payments-{Guid.NewGuid()}")
            .Options);

    private static IPayPalGateway FakeGateway()
    {
        var gateway = Substitute.For<IPayPalGateway>();
        gateway.Currency.Returns("USD");
        gateway.AuthorizeAsync(Arg.Any<AuthorizeCardPaymentCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => new PayPalAuthorizationResult
            {
                PayPalOrderId = "ORD-X",
                OrderStatus = "COMPLETED",
                AuthorizationId = "AUTH-X",
                AuthorizationStatus = "CREATED",
                AuthorizedAmount = ((AuthorizeCardPaymentCommand)ci[0]).Amount,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(29)
            });
        gateway.GetAuthorizationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalAuthorizationState { AuthorizationId = "AUTH-X", Status = "CREATED", ExpiresAt = DateTimeOffset.UtcNow.AddDays(29) });
        gateway.CaptureAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new PayPalCaptureResult
            {
                CaptureId = "CAP-X",
                Status = "COMPLETED",
                GrossAmount = (decimal)ci[1],
                PayPalFee = 1.00m,
                NetAmount = (decimal)ci[1] - 1.00m,
                Currency = "USD"
            });
        gateway.RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => new PayPalRefundResult { RefundId = "REF-" + Guid.NewGuid().ToString("N")[..6], Status = "COMPLETED", Amount = (decimal?)ci[1] });
        return gateway;
    }

    private static (PaymentService service, IPayPalGateway gateway, CatalogContext ctx) Build()
    {
        var ctx = NewContext();
        ctx.CatalogItems.Add(new CatalogItem(1, 1, "desc", "Widget", 19.50m, "pic.png"));
        ctx.SaveChanges();
        var catalogItemId = ctx.CatalogItems.First().Id;

        var gateway = FakeGateway();
        var uri = Substitute.For<IUriComposer>();
        uri.ComposePicUri(Arg.Any<string>()).Returns(ci => (string)ci[0]);

        var service = new PaymentService(
            new EfRepository<Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate.Order>(ctx),
            new EfRepository<Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate.PaymentMethod>(ctx),
            new EfRepository<CatalogItem>(ctx),
            gateway, uri, NullLogger<PaymentService>.Instance);

        _ = catalogItemId;
        return (service, gateway, ctx);
    }

    private static ShipTo Addr => new("1 St", "City", "CA", "USA", "95131");

    private static CardDetails Card => new()
    {
        Number = "4111111111111111",
        Expiry = "2030-01",
        SecurityCode = "123",
        BillingAddress = new CardBillingAddress { CountryCode = "US" }
    };

    [Fact]
    public async Task Place_then_pay_authorizes_the_order()
    {
        var (service, _, _) = Build();
        var orderId = await service.PlaceOrderAsync(Buyer, new List<OrderLine> { new(1, 2) }, Addr, CancellationToken.None);

        var view = await service.PayAsync(Buyer, orderId, new PayCommand { Card = Card }, CancellationToken.None);

        Assert.Equal("Authorized", view.Status);
        Assert.Equal(39.00m, view.Total);
        Assert.Equal("AUTH-X", view.AuthorizationId);
        Assert.Equal(39.00m, view.AuthorizedAmount);
    }

    [Fact]
    public async Task Another_shopper_cannot_pay_someone_elses_order()
    {
        var (service, _, _) = Build();
        var orderId = await service.PlaceOrderAsync(Buyer, new List<OrderLine> { new(1, 1) }, Addr, CancellationToken.None);

        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => service.PayAsync("intruder@example.com", orderId, new PayCommand { Card = Card }, CancellationToken.None));
    }

    [Fact]
    public async Task Repeating_a_refund_under_the_same_key_does_not_refund_twice()
    {
        var (service, gateway, _) = Build();
        var orderId = await service.PlaceOrderAsync(Buyer, new List<OrderLine> { new(1, 2) }, Addr, CancellationToken.None); // 39.00
        await service.PayAsync(Buyer, orderId, new PayCommand { Card = Card }, CancellationToken.None);
        await service.FulfilAsync(orderId, CancellationToken.None);

        var first = await service.RefundAsync(orderId, 10.00m, "r1", CancellationToken.None);
        var second = await service.RefundAsync(orderId, 10.00m, "r1", CancellationToken.None);

        Assert.Equal(first.RefundId, second.RefundId);
        await gateway.Received(1).RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal(10.00m, second.Order.RefundedAmount);
    }

    [Fact]
    public async Task Two_distinct_partial_refunds_both_apply()
    {
        var (service, gateway, _) = Build();
        var orderId = await service.PlaceOrderAsync(Buyer, new List<OrderLine> { new(1, 2) }, Addr, CancellationToken.None); // 39.00
        await service.PayAsync(Buyer, orderId, new PayCommand { Card = Card }, CancellationToken.None);
        await service.FulfilAsync(orderId, CancellationToken.None);

        await service.RefundAsync(orderId, 10.00m, "r1", CancellationToken.None);
        var second = await service.RefundAsync(orderId, 5.00m, "r2", CancellationToken.None);

        Assert.Equal(15.00m, second.Order.RefundedAmount);
        await gateway.Received(2).RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refund_beyond_captured_is_rejected()
    {
        var (service, _, _) = Build();
        var orderId = await service.PlaceOrderAsync(Buyer, new List<OrderLine> { new(1, 1) }, Addr, CancellationToken.None); // 19.50
        await service.PayAsync(Buyer, orderId, new PayCommand { Card = Card }, CancellationToken.None);
        await service.FulfilAsync(orderId, CancellationToken.None);

        await Assert.ThrowsAsync<OverRefundException>(
            () => service.RefundAsync(orderId, 50.00m, "r1", CancellationToken.None));
    }

    [Fact]
    public async Task Fulfil_captures_the_exact_total_with_fee_and_net()
    {
        var (service, _, _) = Build();
        var orderId = await service.PlaceOrderAsync(Buyer, new List<OrderLine> { new(1, 2) }, Addr, CancellationToken.None); // 39.00
        await service.PayAsync(Buyer, orderId, new PayCommand { Card = Card }, CancellationToken.None);

        var view = await service.FulfilAsync(orderId, CancellationToken.None);

        Assert.Equal("Fulfilled", view.Status);
        Assert.Equal(39.00m, view.CapturedAmount);
        Assert.Equal(1.00m, view.PayPalFee);
        Assert.Equal(38.00m, view.NetProceeds);
    }
}
