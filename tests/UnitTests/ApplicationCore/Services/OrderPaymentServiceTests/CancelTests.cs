using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.OrderPaymentServiceTests;

public class CancelTests : OrderPaymentServiceTestBase
{
    [Fact]
    public async Task UnpaidOrder_Cancels_WithoutCallingPayPal()
    {
        var order = NewOrder(); // AwaitingPayment
        OrderRepo.GetByIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(order);

        var result = await CreateService().CancelAsync(OrderId, CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, result.Status);
        await Gateway.DidNotReceive().VoidAuthorizationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizedOrder_Cancels_AndVoidsHold()
    {
        var order = NewOrder();
        order.MarkAuthorized();
        OrderRepo.GetByIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(order);
        PaymentRepo.FirstOrDefaultAsync(Arg.Any<OrderPaymentByOrderIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(PaymentFor(20m));

        var result = await CreateService().CancelAsync(OrderId, CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, result.Status);
        await Gateway.Received(1).VoidAuthorizationAsync(Arg.Any<string>(), "AUTH1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FulfilledOrder_Throws409()
    {
        var order = NewOrder();
        order.MarkAuthorized();
        order.MarkFulfilled();
        OrderRepo.GetByIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(order);

        await Assert.ThrowsAsync<OrderStateConflictException>(() =>
            CreateService().CancelAsync(OrderId, CancellationToken.None));
    }

    [Fact]
    public async Task AlreadyCancelled_IsIdempotentNoOp()
    {
        var order = NewOrder();
        order.MarkCancelled();
        OrderRepo.GetByIdAsync(OrderId, Arg.Any<CancellationToken>()).Returns(order);

        var result = await CreateService().CancelAsync(OrderId, CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, result.Status);
        await Gateway.DidNotReceive().VoidAuthorizationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
