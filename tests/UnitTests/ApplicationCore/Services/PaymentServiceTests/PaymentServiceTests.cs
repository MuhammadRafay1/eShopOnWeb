using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class PaymentServiceTests
{
    private const string BuyerId = "demouser@microsoft.com";
    private const string OtherBuyer = "someone-else@microsoft.com";

    private readonly IRepository<Order> _orders = Substitute.For<IRepository<Order>>();
    private readonly IRepository<Payment> _payments = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<PaymentMethod> _methods = Substitute.For<IRepository<PaymentMethod>>();
    private readonly IPayPalGateway _paypal = Substitute.For<IPayPalGateway>();
    private readonly IAppLogger<PaymentService> _logger = Substitute.For<IAppLogger<PaymentService>>();
    private readonly PayPalOptions _options = new() { Currency = "USD", Environment = "sandbox" };

    private PaymentService CreateService() => new(_orders, _payments, _methods, _paypal, _options, _logger);

    private static Order OrderFor(string buyerId, OrderStatus status)
    {
        var address = new Address("123 Main St.", "Kent", "OH", "United States", "44240");
        var itemOrdered = new CatalogItemOrdered(1, "Test Item", "pic.png");
        var order = new Order(buyerId, address, new List<OrderItem> { new(itemOrdered, 10m, 2) });
        order.SetStatus(status);
        return order;
    }

    private static Payment AuthorizedPayment(int orderId = 1) =>
        new(orderId, "USD", 20m, "PP-ORDER", "ESHOP-1-abc", "AUTH-1", "CREATED",
            DateTimeOffset.UtcNow.AddDays(29), "auth-req", null);

    private static Payment CapturedPayment(int orderId = 1)
    {
        var payment = AuthorizedPayment(orderId);
        payment.RecordCapture("CAP-1", "COMPLETED", 20m, 1m, 19m, "cap-req");
        return payment;
    }

    // --- Fulfil: stale-authorization renewal (cannot be exercised against live PayPal in a session) ---

    [Fact]
    public async Task Fulfil_ReauthorizesAndRetries_WhenAuthorizationExpired()
    {
        var order = OrderFor(BuyerId, OrderStatus.PaymentAuthorized);
        var payment = AuthorizedPayment();
        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), Arg.Any<CancellationToken>()).Returns(payment);

        // First capture (against the stale auth) reports expired; capture against the renewed auth succeeds.
        _paypal.CaptureAsync("AUTH-1", Arg.Any<decimal>(), "USD", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PayPalAuthorizationExpiredException("AUTHORIZATION_EXPIRED", "expired"));
        _paypal.ReauthorizeAsync("AUTH-1", Arg.Any<decimal>(), "USD", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalAuthorizationResult(string.Empty, "AUTH-2", "CREATED", DateTimeOffset.UtcNow.AddDays(3)));
        _paypal.CaptureAsync("AUTH-2", Arg.Any<decimal>(), "USD", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalCaptureResult("CAP-2", "COMPLETED", 20m, 1.20m, 18.80m));

        var result = await CreateService().FulfilOrderAsync(1);

        Assert.Equal("CAP-2", result.PayPalCaptureId);
        Assert.Equal("AUTH-2", result.PayPalAuthorizationId);
        Assert.Equal(18.80m, result.NetAmount);
        Assert.Equal(OrderStatus.Fulfilled, order.Status);
        await _paypal.Received(1).ReauthorizeAsync("AUTH-1", Arg.Any<decimal>(), "USD", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fulfil_Throws_WhenAuthorizationNotRenewable()
    {
        var order = OrderFor(BuyerId, OrderStatus.PaymentAuthorized);
        var payment = AuthorizedPayment();
        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), Arg.Any<CancellationToken>()).Returns(payment);

        _paypal.CaptureAsync("AUTH-1", Arg.Any<decimal>(), "USD", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PayPalAuthorizationExpiredException("AUTHORIZATION_EXPIRED", "expired"));
        _paypal.ReauthorizeAsync("AUTH-1", Arg.Any<decimal>(), "USD", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PayPalApiException(422, "UNPROCESSABLE_ENTITY", "cannot reauthorize", "dbg", "REAUTHORIZATION_TOO_LATE", "too old"));

        var ex = await Assert.ThrowsAsync<AuthorizationNotRenewableException>(() => CreateService().FulfilOrderAsync(1));
        Assert.Equal("REAUTHORIZATION_TOO_LATE", ex.Issue);
        Assert.Equal(OrderStatus.PaymentAuthorized, order.Status); // unchanged, operator must act
    }

    [Fact]
    public async Task Fulfil_IsIdempotent_WhenAlreadyCaptured()
    {
        var order = OrderFor(BuyerId, OrderStatus.Fulfilled);
        var payment = CapturedPayment();
        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), Arg.Any<CancellationToken>()).Returns(payment);

        var result = await CreateService().FulfilOrderAsync(1);

        Assert.Equal("CAP-1", result.PayPalCaptureId);
        await _paypal.DidNotReceive().CaptureAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Refund: idempotency and the captured-amount cap ---

    [Fact]
    public async Task Refund_RepeatedKey_DoesNotRefundTwice()
    {
        var order = OrderFor(BuyerId, OrderStatus.PartiallyRefunded);
        var payment = CapturedPayment();
        payment.RecordRefund("PP-REF-1", 5m, "COMPLETED", "key-1");
        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), Arg.Any<CancellationToken>()).Returns(payment);

        var receipt = await CreateService().RefundOrderAsync(1, BuyerId, "key-1", 5m);

        Assert.Equal(5m, receipt.Amount);
        await _paypal.DidNotReceive().RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refund_DistinctKeys_AreSeparateRefunds()
    {
        var order = OrderFor(BuyerId, OrderStatus.Fulfilled);
        var payment = CapturedPayment();
        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), Arg.Any<CancellationToken>()).Returns(payment);
        _paypal.RefundAsync("CAP-1", 5m, "USD", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PayPalRefundResult("PP-REF-2", "COMPLETED", 5m, 5m));

        var receipt = await CreateService().RefundOrderAsync(1, BuyerId, "key-2", 5m);

        Assert.Equal("COMPLETED", receipt.Status);
        Assert.Equal(15m, receipt.RemainingRefundable);
        Assert.Equal(OrderStatus.PartiallyRefunded.ToString(), receipt.OrderStatus);
        await _paypal.Received(1).RefundAsync("CAP-1", 5m, "USD", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refund_BeyondCaptured_IsRejectedBeforeCallingPayPal()
    {
        var order = OrderFor(BuyerId, OrderStatus.Fulfilled);
        var payment = CapturedPayment();
        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), Arg.Any<CancellationToken>()).Returns(payment);

        await Assert.ThrowsAsync<RefundAmountExceedsRemainingException>(
            () => CreateService().RefundOrderAsync(1, BuyerId, "key-x", 999m));
        await _paypal.DidNotReceive().RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Ownership ---

    [Fact]
    public async Task Refund_OnAnotherShoppersOrder_IsNotFound()
    {
        var order = OrderFor(OtherBuyer, OrderStatus.Fulfilled);
        _orders.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(order);

        await Assert.ThrowsAsync<OrderNotFoundException>(
            () => CreateService().RefundOrderAsync(1, BuyerId, "key-1", 1m));
    }

    [Fact]
    public async Task DeleteSavedCard_OfAnotherShopper_IsNotFound()
    {
        var method = new PaymentMethod(OtherBuyer, "VAULT-1", "CUST", "VISA", "1111", "2030-01");
        _methods.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(method);

        await Assert.ThrowsAsync<PaymentMethodNotFoundException>(
            () => CreateService().DeleteSavedCardAsync(7, BuyerId));
        await _paypal.DidNotReceive().DeleteVaultedCardAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Authorize idempotency guard ---

    [Fact]
    public async Task Pay_OnAlreadyAuthorizedOrder_ReturnsExistingPayment_WithoutCallingPayPal()
    {
        var order = OrderFor(BuyerId, OrderStatus.PaymentAuthorized);
        var payment = AuthorizedPayment();
        _orders.FirstOrDefaultAsync(Arg.Any<OrderWithItemsByIdSpec>(), Arg.Any<CancellationToken>()).Returns(order);
        _payments.FirstOrDefaultAsync(Arg.Any<PaymentByOrderIdSpec>(), Arg.Any<CancellationToken>()).Returns(payment);

        var card = new PaymentCard("4111111111111111", "2030-01", "123", "T", "123 Main", null, "Kent", "OH", "44240", "US");
        var result = await CreateService().AuthorizeOrderAsync(1, BuyerId, card, null);

        Assert.Equal("AUTH-1", result.PayPalAuthorizationId);
        await _paypal.DidNotReceive().AuthorizeWithCardAsync(Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PaymentCard>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pay_RequiresExactlyOnePaymentSource()
    {
        var card = new PaymentCard("4111111111111111", "2030-01", "123", "T", "123 Main", null, "Kent", "OH", "44240", "US");
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().AuthorizeOrderAsync(1, BuyerId, card, 5));
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().AuthorizeOrderAsync(1, BuyerId, null, null));
    }

    // --- Reconciliation matching (matched / PayPal-only / eShop-only) ---

    [Fact]
    public async Task Reconcile_LinesUpPayPalTransactionsAgainstLocalPayments()
    {
        var matchedPayment = CapturedPayment(1); // InvoiceId "ESHOP-1-abc", capture "CAP-1"
        var unmatchedPayment = new Payment(2, "USD", 30m, "PP-ORDER-2", "ESHOP-2-xyz", "AUTH-9", "CREATED", null, "auth-req-2", null);

        _payments.ListAsync(Arg.Any<PaymentsInDateRangeSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<Payment> { matchedPayment, unmatchedPayment });

        _paypal.SearchTransactionsAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<PayPalTransaction>
            {
                new("TXN-1", "CAP-1", "TXN", "ESHOP-1-abc", 20m, "USD", 1m, "S", DateTimeOffset.UtcNow),
                new("TXN-2", "UNKNOWN-REF", "TXN", "ESHOP-UNKNOWN", 99m, "USD", 2m, "S", DateTimeOffset.UtcNow)
            });

        var report = await CreateService().ReconcileAsync(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow);

        Assert.Single(report.Matched);
        Assert.Equal(1, report.Matched[0].OrderId);
        Assert.Single(report.PayPalOnly);
        Assert.Equal("TXN-2", report.PayPalOnly[0].PayPalTransactionId);
        Assert.Single(report.EShopOnly);
        Assert.Equal(2, report.EShopOnly[0].OrderId);
    }

    [Fact]
    public async Task Reconcile_RejectsInvertedRange()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().ReconcileAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1)));
    }
}
