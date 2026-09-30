using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.OrderPaymentServiceTests;

public class AuthorizeTests : OrderPaymentServiceTestBase
{
    private void ArrangeOrder(Order order) =>
        OrderRepo.FirstOrDefaultAsync(Arg.Any<OrderWithItemsByIdSpec>(), Arg.Any<CancellationToken>()).Returns(order);

    [Fact]
    public async Task AuthorizesWithCardAndMarksOrderAuthorized()
    {
        var order = NewOrder();
        ArrangeOrder(order);
        Gateway.AuthorizeWithCardAsync(Arg.Any<string>(), OrderId, order.Total(), "USD", Arg.Any<CardDetails>(), Arg.Any<CancellationToken>())
            .Returns(AuthResult(order.Total()));
        PaymentRepo.AddAsync(Arg.Any<OrderPayment>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<OrderPayment>());

        var result = await CreateService().AuthorizeAsync(OrderId, BuyerId, SampleCard(), null, CancellationToken.None);

        Assert.Equal(OrderStatus.Authorized, result.Status);
        Assert.Equal(order.Total(), result.AuthorizedAmount);
        Assert.Equal(OrderStatus.Authorized, order.Status);
        await Gateway.Received(1).AuthorizeWithCardAsync(Arg.Any<string>(), OrderId, order.Total(), "USD", Arg.Any<CardDetails>(), Arg.Any<CancellationToken>());
        await PaymentRepo.Received(1).AddAsync(Arg.Any<OrderPayment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoubleClick_ReturnsExistingPayment_WithoutCallingPayPal()
    {
        var order = NewOrder();
        order.MarkAuthorized();
        ArrangeOrder(order);
        PaymentRepo.FirstOrDefaultAsync(Arg.Any<OrderPaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(PaymentFor(20m));

        var result = await CreateService().AuthorizeAsync(OrderId, BuyerId, SampleCard(), null, CancellationToken.None);

        Assert.Equal(20m, result.AuthorizedAmount);
        await Gateway.DidNotReceive().AuthorizeWithCardAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CardDetails>(), Arg.Any<CancellationToken>());
        await PaymentRepo.DidNotReceive().AddAsync(Arg.Any<OrderPayment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotOwnedOrder_Throws404()
    {
        var order = NewOrder(); // BuyerId = "12345"
        ArrangeOrder(order);

        await Assert.ThrowsAsync<OrderNotFoundException>(() =>
            CreateService().AuthorizeAsync(OrderId, OtherBuyerId, SampleCard(), null, CancellationToken.None));
    }

    [Fact]
    public async Task MissingOrder_Throws404()
    {
        await Assert.ThrowsAsync<OrderNotFoundException>(() =>
            CreateService().AuthorizeAsync(OrderId, BuyerId, SampleCard(), null, CancellationToken.None));
    }

    [Fact]
    public async Task BothCardAndSavedCard_Throws400()
    {
        ArrangeOrder(NewOrder());
        await Assert.ThrowsAsync<InvalidPaymentRequestException>(() =>
            CreateService().AuthorizeAsync(OrderId, BuyerId, SampleCard(), 5, CancellationToken.None));
    }

    [Fact]
    public async Task NeitherCardNorSavedCard_Throws400()
    {
        ArrangeOrder(NewOrder());
        await Assert.ThrowsAsync<InvalidPaymentRequestException>(() =>
            CreateService().AuthorizeAsync(OrderId, BuyerId, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task CancelledOrder_ThrowsConflict()
    {
        var order = NewOrder();
        order.MarkCancelled();
        ArrangeOrder(order);

        await Assert.ThrowsAsync<OrderStateConflictException>(() =>
            CreateService().AuthorizeAsync(OrderId, BuyerId, SampleCard(), null, CancellationToken.None));
    }

    [Fact]
    public async Task SavedCardNotOwned_Throws404()
    {
        ArrangeOrder(NewOrder());
        var othersCard = new SavedPaymentMethod(OtherBuyerId, "VAULT1", "VISA", "1111", "2030-12", "X");
        CardRepo.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(othersCard);

        await Assert.ThrowsAsync<PaymentMethodNotFoundException>(() =>
            CreateService().AuthorizeAsync(OrderId, BuyerId, null, 7, CancellationToken.None));
    }

    [Fact]
    public async Task PayerActionRequired_Throws_AndPersistsNoPayment()
    {
        var order = NewOrder();
        ArrangeOrder(order);
        Gateway.AuthorizeWithCardAsync(Arg.Any<string>(), OrderId, order.Total(), "USD", Arg.Any<CardDetails>(), Arg.Any<CancellationToken>())
            .Returns(new AuthorizationResult("PPO", "", "PAYER_ACTION_REQUIRED", order.Total(), DateTimeOffset.UtcNow, true));

        await Assert.ThrowsAsync<PayerActionRequiredException>(() =>
            CreateService().AuthorizeAsync(OrderId, BuyerId, SampleCard(), null, CancellationToken.None));

        await PaymentRepo.DidNotReceive().AddAsync(Arg.Any<OrderPayment>(), Arg.Any<CancellationToken>());
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
    }

    [Fact]
    public async Task PaysWithSavedCard_WhenOwned()
    {
        var order = NewOrder();
        ArrangeOrder(order);
        var myCard = new SavedPaymentMethod(BuyerId, "VAULT9", "VISA", "1111", "2030-12", "Me");
        CardRepo.GetByIdAsync(9, Arg.Any<CancellationToken>()).Returns(myCard);
        Gateway.AuthorizeWithVaultedCardAsync(Arg.Any<string>(), OrderId, order.Total(), "USD", "VAULT9", Arg.Any<CancellationToken>())
            .Returns(AuthResult(order.Total()));
        PaymentRepo.AddAsync(Arg.Any<OrderPayment>(), Arg.Any<CancellationToken>()).Returns(ci => ci.Arg<OrderPayment>());

        var result = await CreateService().AuthorizeAsync(OrderId, BuyerId, null, 9, CancellationToken.None);

        Assert.Equal(OrderStatus.Authorized, result.Status);
        await Gateway.Received(1).AuthorizeWithVaultedCardAsync(Arg.Any<string>(), OrderId, order.Total(), "USD", "VAULT9", Arg.Any<CancellationToken>());
    }
}
