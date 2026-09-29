using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.BuyerAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class OrderPaymentServiceTests
{
    private const string Buyer = "buyer@x.com";

    private readonly IRepository<Order> _orders = Substitute.For<IRepository<Order>>();
    private readonly IRepository<Buyer> _buyers = Substitute.For<IRepository<Buyer>>();
    private readonly IPayPalOrdersClient _ordersClient = Substitute.For<IPayPalOrdersClient>();
    private readonly IPayPalPaymentsClient _paymentsClient = Substitute.For<IPayPalPaymentsClient>();
    private readonly IPayPalCurrencyProvider _currency = Substitute.For<IPayPalCurrencyProvider>();
    private readonly IAppLogger<OrderPaymentService> _logger = Substitute.For<IAppLogger<OrderPaymentService>>();

    private OrderPaymentService CreateService()
    {
        _currency.Currency.Returns("USD");
        return new OrderPaymentService(_orders, _buyers, _ordersClient, _paymentsClient, _currency, _logger);
    }

    private static Order NewOrder(string buyer = Buyer, decimal unit = 10m, int qty = 2)
    {
        var items = new List<OrderItem> { new OrderItem(new CatalogItemOrdered(1, "Item", "pic.png"), unit, qty) };
        return new Order(buyer, new Address("s", "c", "st", "US", "12345"), items);
    }

    private void OrderRepoReturns(Order? order) =>
        _orders.FirstOrDefaultAsync(Arg.Any<OrderWithPaymentByIdSpec>(), Arg.Any<CancellationToken>()).Returns(order);

    // ---- AuthorizeAsync ----

    [Fact]
    public async Task Authorize_BothCardAndSavedCard_Throws()
    {
        var svc = CreateService();
        var req = new PaymentAuthorizationRequest { Card = new CardDetails(), PaymentMethodId = 5 };
        await Assert.ThrowsAsync<InvalidPaymentRequestException>(() => svc.AuthorizeAsync(1, Buyer, req));
    }

    [Fact]
    public async Task Authorize_NeitherCardNorSavedCard_Throws()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<InvalidPaymentRequestException>(
            () => svc.AuthorizeAsync(1, Buyer, new PaymentAuthorizationRequest()));
    }

    [Fact]
    public async Task Authorize_OrderOwnedByAnother_ThrowsNotFound()
    {
        OrderRepoReturns(NewOrder(buyer: "someone-else"));
        var svc = CreateService();
        var req = new PaymentAuthorizationRequest { Card = new CardDetails() };
        await Assert.ThrowsAsync<OrderNotFoundException>(() => svc.AuthorizeAsync(1, Buyer, req));
    }

    [Fact]
    public async Task Authorize_HappyPath_PlacesHoldAndPersists()
    {
        var order = NewOrder(); // total 20
        OrderRepoReturns(order);
        _ordersClient.CreateOrderAsync(Arg.Any<CreateOrderInput>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalOrderResult { Id = "PPO", Status = "CREATED", InvoiceId = "eshop-order-1-run" });
        _ordersClient.AuthorizeOrderAsync(Arg.Any<int>(), "PPO", Arg.Any<CancellationToken>())
            .Returns(new PayPalAuthorizationResult { Id = "AUTH", Status = "CREATED", Amount = 20m, CurrencyCode = "USD" });

        var svc = CreateService();
        var payment = await svc.AuthorizeAsync(1, Buyer, new PaymentAuthorizationRequest { Card = new CardDetails() });

        Assert.Equal("PPO", payment.PayPalOrderId);
        Assert.Equal("AUTH", payment.PayPalAuthorizationId);
        Assert.Equal(20m, payment.AuthorizedAmount);
        Assert.Equal(OrderStatus.Authorized, order.Status);
        await _orders.Received(1).UpdateAsync(order, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_AlreadyAuthorized_IsIdempotent_NoPayPalCall()
    {
        var order = NewOrder();
        var existing = new OrderPayment("PPO", "USD", 20m);
        existing.RecordAuthorization("AUTH", "CREATED", DateTimeOffset.UtcNow.AddDays(3));
        order.BeginPayment(existing);
        OrderRepoReturns(order);

        var svc = CreateService();
        var result = await svc.AuthorizeAsync(1, Buyer, new PaymentAuthorizationRequest { Card = new CardDetails() });

        Assert.Same(existing, result);
        await _ordersClient.DidNotReceive().CreateOrderAsync(Arg.Any<CreateOrderInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authorize_PayerActionRequired_Throws()
    {
        OrderRepoReturns(NewOrder());
        _ordersClient.CreateOrderAsync(Arg.Any<CreateOrderInput>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalOrderResult { Id = "PPO", Status = "PAYER_ACTION_REQUIRED", PayerActionRequired = true });

        var svc = CreateService();
        await Assert.ThrowsAsync<PayPalPayerActionRequiredException>(
            () => svc.AuthorizeAsync(1, Buyer, new PaymentAuthorizationRequest { Card = new CardDetails() }));
    }

    [Fact]
    public async Task Authorize_WithSavedCard_ResolvesVaultIdAndValidatesOwnership()
    {
        var order = NewOrder();
        OrderRepoReturns(order);

        var buyer = new Buyer(Buyer);
        buyer.AddPaymentMethod(new PaymentMethod("VAULT123", "VISA", "1111", "2028-04", null));
        _buyers.FirstOrDefaultAsync(Arg.Any<BuyerWithPaymentMethodsSpecification>(), Arg.Any<CancellationToken>()).Returns(buyer);

        CreateOrderInput? captured = null;
        _ordersClient.CreateOrderAsync(Arg.Do<CreateOrderInput>(i => captured = i), Arg.Any<CancellationToken>())
            .Returns(new PayPalOrderResult { Id = "PPO", Status = "CREATED" });
        _ordersClient.AuthorizeOrderAsync(Arg.Any<int>(), "PPO", Arg.Any<CancellationToken>())
            .Returns(new PayPalAuthorizationResult { Id = "AUTH", Status = "CREATED" });

        var pmId = buyer.PaymentMethods.GetEnumerator();
        pmId.MoveNext();
        var svc = CreateService();
        await svc.AuthorizeAsync(1, Buyer, new PaymentAuthorizationRequest { PaymentMethodId = pmId.Current.Id });

        Assert.Equal("VAULT123", captured!.VaultId);
        Assert.Null(captured.Card);
    }

    [Fact]
    public async Task Authorize_WithUnknownSavedCard_ThrowsNotFound()
    {
        OrderRepoReturns(NewOrder());
        _buyers.FirstOrDefaultAsync(Arg.Any<BuyerWithPaymentMethodsSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new Buyer(Buyer));

        var svc = CreateService();
        await Assert.ThrowsAsync<PaymentMethodNotFoundException>(
            () => svc.AuthorizeAsync(1, Buyer, new PaymentAuthorizationRequest { PaymentMethodId = 999 }));
    }

    // ---- FulfilAsync ----

    private Order AuthorizedOrderWithPayment(out OrderPayment payment)
    {
        var order = NewOrder();
        payment = new OrderPayment("PPO", "USD", 20m);
        payment.RecordAuthorization("AUTH", "CREATED", DateTimeOffset.UtcNow.AddDays(3));
        order.BeginPayment(payment);
        return order;
    }

    [Fact]
    public async Task Fulfil_CapturesAndStoresBreakdown()
    {
        var order = AuthorizedOrderWithPayment(out var payment);
        OrderRepoReturns(order);
        _paymentsClient.CaptureAuthorizationAsync(Arg.Any<int>(), "AUTH", 20m, "USD", Arg.Any<CancellationToken>())
            .Returns(new PayPalCaptureResult { Id = "CAP", Status = "COMPLETED", GrossAmount = 20m, PayPalFeeAmount = 1m, NetAmount = 19m, CurrencyCode = "USD" });

        var svc = CreateService();
        var result = await svc.FulfilAsync(1);

        Assert.Equal("CAP", result.PayPalCaptureId);
        Assert.Equal(20m, result.CapturedAmount);
        Assert.Equal(1m, result.PayPalFeeAmount);
        Assert.Equal(19m, result.NetAmount);
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
    }

    [Fact]
    public async Task Fulfil_StaleAuthorization_ReauthorizesThenCaptures()
    {
        var order = AuthorizedOrderWithPayment(out var payment);
        OrderRepoReturns(order);

        var staleError = new PayPalApiException(422, "UNPROCESSABLE_ENTITY", "stale", "dbg",
            new List<PayPalErrorDetail> { new() { Issue = "AUTHORIZATION_EXPIRED" } });

        // First capture throws (stale); after reauthorize the second capture succeeds.
        _paymentsClient.CaptureAuthorizationAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<PayPalCaptureResult>(
                _ => throw staleError,
                _ => new PayPalCaptureResult { Id = "CAP2", Status = "COMPLETED", GrossAmount = 20m, PayPalFeeAmount = 1m, NetAmount = 19m });
        _paymentsClient.ReauthorizeAuthorizationAsync(Arg.Any<int>(), "AUTH", 20m, "USD", Arg.Any<CancellationToken>())
            .Returns(new PayPalAuthorizationResult { Id = "AUTH2", Status = "CREATED", ExpirationTime = DateTimeOffset.UtcNow.AddDays(3) });

        var svc = CreateService();
        var result = await svc.FulfilAsync(1);

        Assert.Equal("CAP2", result.PayPalCaptureId);
        Assert.Equal("AUTH2", result.PayPalAuthorizationId); // id overwritten from reauthorize
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
        await _paymentsClient.Received(1).ReauthorizeAuthorizationAsync(Arg.Any<int>(), "AUTH", 20m, "USD", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fulfil_ReauthorizeFails_ThrowsFulfilmentException_DoesNotFulfil()
    {
        var order = AuthorizedOrderWithPayment(out _);
        OrderRepoReturns(order);

        var staleError = new PayPalApiException(422, "UNPROCESSABLE_ENTITY", "stale", "dbg",
            new List<PayPalErrorDetail> { new() { Issue = "AUTHORIZATION_EXPIRED" } });
        _paymentsClient.CaptureAuthorizationAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<PayPalCaptureResult>(_ => throw staleError);
        _paymentsClient.ReauthorizeAuthorizationAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<PayPalAuthorizationResult>(_ => throw new PayPalApiException(422, "REAUTHORIZATION_NOT_ALLOWED", "too late", "dbg2", null));

        var svc = CreateService();
        await Assert.ThrowsAsync<OrderFulfilmentException>(() => svc.FulfilAsync(1));
        Assert.Equal(OrderStatus.Authorized, order.Status); // not marked fulfilled
    }

    [Fact]
    public async Task Fulfil_AlreadyFulfilled_IsIdempotent()
    {
        var order = AuthorizedOrderWithPayment(out var payment);
        payment.RecordCapture("CAP", "COMPLETED", 20m, 1m, 19m);
        order.MarkFulfilled();
        OrderRepoReturns(order);

        var svc = CreateService();
        var result = await svc.FulfilAsync(1);

        Assert.Same(payment, result);
        await _paymentsClient.DidNotReceive().CaptureAuthorizationAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- CancelAsync ----

    [Fact]
    public async Task Cancel_VoidsHoldAndMarksCancelled()
    {
        var order = AuthorizedOrderWithPayment(out _);
        OrderRepoReturns(order);

        var svc = CreateService();
        await svc.CancelAsync(1);

        await _paymentsClient.Received(1).VoidAuthorizationAsync(Arg.Any<int>(), "AUTH", Arg.Any<CancellationToken>());
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    // ---- RefundAsync ----

    private Order FulfilledOrder(out OrderPayment payment, decimal captured = 100m)
    {
        var order = NewOrder(unit: captured, qty: 1);
        payment = new OrderPayment("PPO", "USD", captured);
        payment.RecordAuthorization("AUTH", "CREATED", DateTimeOffset.UtcNow.AddDays(3));
        payment.RecordCapture("CAP", "COMPLETED", captured, 3m, captured - 3m);
        order.BeginPayment(payment);
        order.MarkFulfilled();
        return order;
    }

    [Fact]
    public async Task Refund_ReplayUnderSameKey_ReturnsCached_NoPayPalCall()
    {
        var order = FulfilledOrder(out var payment);
        payment.AddRefund(new Refund("R1", 10m, "USD", "COMPLETED", "K1"));
        OrderRepoReturns(order);

        var svc = CreateService();
        var result = await svc.RefundAsync(1, Buyer, new RefundRequest { IdempotencyKey = "K1", Amount = 10m });

        Assert.Equal("R1", result.PayPalRefundId);
        await _paymentsClient.DidNotReceive().RefundCaptureAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refund_ExceedingRemaining_Throws()
    {
        var order = FulfilledOrder(out var payment);
        payment.AddRefund(new Refund("R1", 90m, "USD", "COMPLETED", "K1"));
        OrderRepoReturns(order);

        var svc = CreateService();
        await Assert.ThrowsAsync<InvalidPaymentRequestException>(
            () => svc.RefundAsync(1, Buyer, new RefundRequest { IdempotencyKey = "K2", Amount = 20m }));
    }

    [Fact]
    public async Task Refund_TwoDistinctKeys_AreTwoRefunds()
    {
        var order = FulfilledOrder(out var payment);
        OrderRepoReturns(order);
        _paymentsClient.RefundCaptureAsync("CAP", Arg.Any<decimal?>(), "USD", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => new PayPalRefundResult { Id = "R1", Status = "COMPLETED", Amount = 10m, CurrencyCode = "USD" },
                _ => new PayPalRefundResult { Id = "R2", Status = "COMPLETED", Amount = 5m, CurrencyCode = "USD" });

        var svc = CreateService();
        var r1 = await svc.RefundAsync(1, Buyer, new RefundRequest { IdempotencyKey = "K1", Amount = 10m });
        var r2 = await svc.RefundAsync(1, Buyer, new RefundRequest { IdempotencyKey = "K2", Amount = 5m });

        Assert.Equal("R1", r1.PayPalRefundId);
        Assert.Equal("R2", r2.PayPalRefundId);
        Assert.Equal(15m, payment.RefundedAmount);
        Assert.Equal(OrderStatus.PartiallyRefunded, order.Status);
    }

    [Fact]
    public async Task Refund_UnfulfilledOrder_Throws()
    {
        var order = AuthorizedOrderWithPayment(out _); // authorized, not captured
        OrderRepoReturns(order);

        var svc = CreateService();
        await Assert.ThrowsAsync<InvalidPaymentRequestException>(
            () => svc.RefundAsync(1, Buyer, new RefundRequest { IdempotencyKey = "K1", Amount = 5m }));
    }

    [Fact]
    public async Task Refund_FullRemaining_MarksRefunded()
    {
        var order = FulfilledOrder(out var payment, captured: 50m);
        OrderRepoReturns(order);
        _paymentsClient.RefundCaptureAsync("CAP", Arg.Any<decimal?>(), "USD", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalRefundResult { Id = "R1", Status = "COMPLETED", Amount = 50m, CurrencyCode = "USD" });

        var svc = CreateService();
        await svc.RefundAsync(1, Buyer, new RefundRequest { IdempotencyKey = "K1" }); // no amount => full

        Assert.Equal(OrderStatus.Refunded, order.Status);
    }
}
