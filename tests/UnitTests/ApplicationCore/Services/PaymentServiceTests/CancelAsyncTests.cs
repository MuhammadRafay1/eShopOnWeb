using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class CancelAsyncTests
{
    private readonly IRepository<Payment> _paymentRepository = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<SavedPaymentMethod> _paymentMethodRepository = Substitute.For<IRepository<SavedPaymentMethod>>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly PaymentService _sut;

    public CancelAsyncTests()
    {
        _sut = new PaymentService(_paymentRepository, _paymentMethodRepository, _gateway);
    }

    private Payment SetUpPayment()
    {
        var payment = new Payment(1, "buyer@example.com", 8.50m, "USD", "ESHOP-1-abc");
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>()).Returns(payment);
        return payment;
    }

    [Fact]
    public async Task CancelingBeforeAnyAuthorizationNeverCallsPayPal()
    {
        SetUpPayment();

        var result = await _sut.CancelAsync(1);

        Assert.Equal("Canceled", result!.Status);
        await _gateway.DidNotReceiveWithAnyArgs().VoidAsync(default!, default!);
    }

    [Fact]
    public async Task CancelingAnAuthorizedOrderVoidsTheHold()
    {
        var payment = SetUpPayment();
        payment.BeginAuthorization("key-1");
        payment.MarkAuthorized("AUTH-1", "CREATED", 8.50m, null);

        var result = await _sut.CancelAsync(1);

        Assert.Equal("Canceled", result!.Status);
        await _gateway.Received(1).VoidAsync("AUTH-1", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelingAFulfilledOrderThrows()
    {
        var payment = SetUpPayment();
        payment.MarkAuthorized("AUTH-1", "CREATED", 8.50m, null);
        payment.MarkCaptured("CAP-1", "COMPLETED", 8.50m, 0.71m, 7.79m);

        await Assert.ThrowsAsync<PaymentStateConflictException>(() => _sut.CancelAsync(1));
    }

    [Fact]
    public async Task CancelingTwiceIsIdempotent()
    {
        var payment = SetUpPayment();
        payment.MarkAuthorized("AUTH-1", "CREATED", 8.50m, null);
        await _sut.CancelAsync(1);

        var second = await _sut.CancelAsync(1);

        Assert.Equal("Canceled", second!.Status);
        await _gateway.Received(1).VoidAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNullWhenNoSuchOrderExists()
    {
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>()).Returns((Payment?)null);

        var result = await _sut.CancelAsync(999);

        Assert.Null(result);
    }
}
