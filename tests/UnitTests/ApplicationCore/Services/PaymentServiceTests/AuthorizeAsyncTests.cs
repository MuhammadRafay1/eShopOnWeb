using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class AuthorizeAsyncTests
{
    private readonly IRepository<Payment> _paymentRepository = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<SavedPaymentMethod> _paymentMethodRepository = Substitute.For<IRepository<SavedPaymentMethod>>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly PaymentService _sut;

    public AuthorizeAsyncTests()
    {
        _sut = new PaymentService(_paymentRepository, _paymentMethodRepository, _gateway);
    }

    private static Payment NewPayment(string buyerId = "buyer@example.com", int orderId = 1) =>
        new(orderId, buyerId, 8.50m, "USD", "ESHOP-1-abc");

    private static CardDetails AnyCard() => new() { Number = "4111111111111111", Expiry = "2030-01", SecurityCode = "123" };

    [Fact]
    public async Task ADoubleClickDoesNotAuthorizeTwice()
    {
        var payment = NewPayment();
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>())
            .Returns(payment);
        _gateway.AuthorizeAsync(Arg.Any<AuthorizeCardPaymentRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthorizationResult { PayPalOrderId = "PPO-1", AuthorizationId = "AUTH-1", Status = "CREATED", HeldAmount = 8.50m });

        var first = await _sut.AuthorizeAsync(1, "buyer@example.com", AnyCard(), null);
        var second = await _sut.AuthorizeAsync(1, "buyer@example.com", AnyCard(), null);

        Assert.Equal("AUTH-1", first!.AuthorizationId);
        Assert.Equal("AUTH-1", second!.AuthorizationId);
        await _gateway.Received(1).AuthorizeAsync(Arg.Any<AuthorizeCardPaymentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNullWhenTheOrderBelongsToAnotherBuyer()
    {
        var payment = NewPayment(buyerId: "owner@example.com");
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>())
            .Returns(payment);

        var result = await _sut.AuthorizeAsync(1, "someone-else@example.com", AnyCard(), null);

        Assert.Null(result);
        await _gateway.DidNotReceive().AuthorizeAsync(Arg.Any<AuthorizeCardPaymentRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNullWhenNoSuchOrderExists()
    {
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>())
            .Returns((Payment?)null);

        var result = await _sut.AuthorizeAsync(999, "buyer@example.com", AnyCard(), null);

        Assert.Null(result);
    }
}
