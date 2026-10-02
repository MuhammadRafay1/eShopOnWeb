using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class OrderPaymentServiceTests
{
    private const string Buyer = "buyer@example.com";

    private readonly IRepository<Order> _orders = Substitute.For<IRepository<Order>>();
    private readonly IRepository<CatalogItem> _catalog = Substitute.For<IRepository<CatalogItem>>();
    private readonly IRepository<OrderPayment> _payments = Substitute.For<IRepository<OrderPayment>>();
    private readonly IRepository<SavedCard> _cards = Substitute.For<IRepository<SavedCard>>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly IAppLogger<OrderPaymentService> _logger = Substitute.For<IAppLogger<OrderPaymentService>>();

    private OrderPaymentService CreateService() =>
        new(_orders, _catalog, _payments, _cards, _gateway, new PaymentLock(), _logger);

    public OrderPaymentServiceTests()
    {
        _gateway.Currency.Returns("USD");
    }

    private OrderPayment NewPayment(decimal amount = 29m) => new(1, Buyer, "USD", amount, "ESHOP-1-abc");

    private void StorePayment(OrderPayment payment) =>
        _payments.FirstOrDefaultAsync(Arg.Any<OrderPaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>()).Returns(payment);

    private static CardInput Card() => new("4111111111111111", "2030-01", "123", "Demo", null);

    [Fact]
    public async Task PayAsync_authorizes_and_marks_authorized()
    {
        var payment = NewPayment();
        StorePayment(payment);
        _gateway.AuthorizeAsync(Arg.Any<AuthorizeGatewayRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthorizationResult("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(28), 29m));

        var view = await CreateService().PayAsync(Buyer, 1, new PayInput { Card = Card() });

        Assert.Equal("Authorized", view.PaymentStatus);
        Assert.Equal("AUTH1", view.AuthorizationId);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    [Fact]
    public async Task PayAsync_is_idempotent_when_already_authorized()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(28));
        StorePayment(payment);

        var view = await CreateService().PayAsync(Buyer, 1, new PayInput { Card = Card() });

        Assert.Equal("Authorized", view.PaymentStatus);
        await _gateway.DidNotReceive().AuthorizeAsync(Arg.Any<AuthorizeGatewayRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayAsync_order_of_another_buyer_is_not_found()
    {
        var payment = new OrderPayment(1, "someone-else@example.com", "USD", 29m, "ESHOP-1-abc");
        StorePayment(payment);

        await Assert.ThrowsAsync<PaymentNotFoundException>(() =>
            CreateService().PayAsync(Buyer, 1, new PayInput { Card = Card() }));
    }

    [Fact]
    public async Task PayAsync_with_saved_card_not_owned_throws_not_found()
    {
        StorePayment(NewPayment());
        _cards.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns(new SavedCard("other@example.com", "vault", "cust", "VISA", "1111", "2030-01", "X"));

        await Assert.ThrowsAsync<PaymentNotFoundException>(() =>
            CreateService().PayAsync(Buyer, 1, new PayInput { SavedPaymentMethodId = 99 }));
    }

    [Fact]
    public async Task PayAsync_decline_marks_failed_and_rotates_reference()
    {
        var payment = NewPayment();
        var reference = payment.PaymentReference;
        StorePayment(payment);
        _gateway.AuthorizeAsync(Arg.Any<AuthorizeGatewayRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthorizationResult>(
                new PaymentGatewayException("declined", 422, isCallerError: true, payPalIssue: "INSTRUMENT_DECLINED")));

        await Assert.ThrowsAsync<PaymentValidationException>(() =>
            CreateService().PayAsync(Buyer, 1, new PayInput { Card = Card() }));

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.NotEqual(reference, payment.PaymentReference); // rotated so a retry is a fresh attempt
    }

    [Fact]
    public async Task PayAsync_gateway_connection_failure_leaves_order_awaiting_payment()
    {
        var payment = NewPayment();
        StorePayment(payment);
        _gateway.AuthorizeAsync(Arg.Any<AuthorizeGatewayRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthorizationResult>(
                new PaymentGatewayException("unreachable", null, isCallerError: false) { OutcomeUnknown = true }));

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            CreateService().PayAsync(Buyer, 1, new PayInput { Card = Card() }));

        // Money may be held — must NOT be marked Failed; the stable key replays on retry.
        Assert.Equal(PaymentStatus.AwaitingPayment, payment.Status);
    }

    [Fact]
    public async Task FulfilAsync_captures_and_records_fee_and_net()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(28));
        StorePayment(payment);
        _gateway.CaptureAsync("AUTH1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CaptureResult("CAP1", "COMPLETED", 29m, 1.24m, 27.76m, "USD"));

        var view = await CreateService().FulfilAsync(1);

        Assert.Equal("Captured", view.PaymentStatus);
        Assert.Equal(1.24m, view.PayPalFee);
        Assert.Equal(27.76m, view.NetAmount);
    }

    [Fact]
    public async Task FulfilAsync_is_idempotent_when_already_captured()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(28));
        payment.MarkCaptured("CAP1", "COMPLETED", 29m, 1.24m, 27.76m);
        StorePayment(payment);

        var view = await CreateService().FulfilAsync(1);

        Assert.Equal("Captured", view.PaymentStatus);
        await _gateway.DidNotReceive().CaptureAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FulfilAsync_stale_authorization_is_reauthorized_before_capture()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(-1)); // expired
        StorePayment(payment);
        _gateway.ReauthorizeAsync("AUTH1", 29m, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AuthorizationSnapshot("AUTH2", "CREATED", DateTimeOffset.UtcNow.AddDays(3)));
        _gateway.CaptureAsync("AUTH2", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CaptureResult("CAP1", "COMPLETED", 29m, 1.24m, 27.76m, "USD"));

        var view = await CreateService().FulfilAsync(1);

        Assert.Equal("Captured", view.PaymentStatus);
        await _gateway.Received().ReauthorizeAsync("AUTH1", 29m, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _gateway.Received().CaptureAsync("AUTH2", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FulfilAsync_reauthorize_failure_throws_operator_actionable_conflict()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(-40)); // too old to renew
        StorePayment(payment);
        _gateway.ReauthorizeAsync("AUTH1", 29m, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthorizationSnapshot>(
                new PaymentGatewayException("expired", 422, isCallerError: true, payPalIssue: "AUTHORIZATION_EXPIRED")));

        var ex = await Assert.ThrowsAsync<PaymentConflictException>(() => CreateService().FulfilAsync(1));
        Assert.Contains("can no longer be renewed", ex.Message);
        await _gateway.DidNotReceive().CaptureAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FulfilAsync_gateway_connection_failure_keeps_order_authorized()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(28));
        StorePayment(payment);
        _gateway.CaptureAsync("AUTH1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CaptureResult>(
                new PaymentGatewayException("unreachable", null, isCallerError: false) { OutcomeUnknown = true }));

        await Assert.ThrowsAsync<PaymentGatewayException>(() => CreateService().FulfilAsync(1));
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    [Fact]
    public async Task CancelAsync_voids_and_marks_cancelled()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(28));
        StorePayment(payment);

        var view = await CreateService().CancelAsync(1);

        Assert.Equal("Cancelled", view.PaymentStatus);
        await _gateway.Received().VoidAsync("AUTH1", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_gateway_connection_failure_keeps_order_authorized()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(28));
        StorePayment(payment);
        _gateway.VoidAsync("AUTH1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PaymentGatewayException("unreachable", null, isCallerError: false) { OutcomeUnknown = true }));

        await Assert.ThrowsAsync<PaymentGatewayException>(() => CreateService().CancelAsync(1));
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    [Fact]
    public async Task RefundAsync_partial_refund_records_refund()
    {
        var payment = CapturedPayment();
        StorePayment(payment);
        _gateway.RefundAsync("CAP1", 5m, "key-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RefundResult("REF1", "COMPLETED", 5m));

        var refund = await CreateService().RefundAsync(Buyer, 1, new RefundInput { Amount = 5m, IdempotencyKey = "key-1" });

        Assert.Equal("REF1", refund.PayPalRefundId);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(5m, payment.TotalRefunded());
    }

    [Fact]
    public async Task RefundAsync_same_key_returns_existing_without_second_call()
    {
        var payment = CapturedPayment();
        StorePayment(payment);
        _gateway.RefundAsync("CAP1", 5m, "key-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RefundResult("REF1", "COMPLETED", 5m));
        var service = CreateService();

        var first = await service.RefundAsync(Buyer, 1, new RefundInput { Amount = 5m, IdempotencyKey = "key-1" });
        var second = await service.RefundAsync(Buyer, 1, new RefundInput { Amount = 5m, IdempotencyKey = "key-1" });

        Assert.Equal(first.RefundId, second.RefundId);
        await _gateway.Received(1).RefundAsync("CAP1", Arg.Any<decimal?>(), "key-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefundAsync_over_refund_throws_validation()
    {
        var payment = CapturedPayment();
        StorePayment(payment);

        await Assert.ThrowsAsync<PaymentValidationException>(() =>
            CreateService().RefundAsync(Buyer, 1, new RefundInput { Amount = 40m, IdempotencyKey = "key-x" }));
    }

    [Fact]
    public async Task RefundAsync_unknown_outcome_marks_claim_unknown_and_settles_on_retry()
    {
        var payment = CapturedPayment();
        StorePayment(payment);
        var calls = 0;
        _gateway.RefundAsync("CAP1", Arg.Any<decimal?>(), "key-1", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                calls++;
                if (calls == 1)
                {
                    throw new PaymentGatewayException("timeout", null, false) { OutcomeUnknown = true };
                }
                return new RefundResult("REF1", "COMPLETED", 5m);
            });
        var service = CreateService();

        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            service.RefundAsync(Buyer, 1, new RefundInput { Amount = 5m, IdempotencyKey = "key-1" }));

        var retry = await service.RefundAsync(Buyer, 1, new RefundInput { Amount = 5m, IdempotencyKey = "key-1" });
        Assert.Equal("REF1", retry.PayPalRefundId);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
    }

    private static OrderPayment CapturedPayment()
    {
        var payment = new OrderPayment(1, Buyer, "USD", 29m, "ESHOP-1-abc");
        payment.MarkAuthorized("PPO1", "AUTH1", "CREATED", DateTimeOffset.UtcNow.AddDays(28));
        payment.MarkCaptured("CAP1", "COMPLETED", 29m, 1.24m, 27.76m);
        return payment;
    }
}
