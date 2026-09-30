using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;
using Microsoft.eShopWeb.ApplicationCore.Services;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class FulfilAsyncTests
{
    private readonly IRepository<Payment> _paymentRepository = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<SavedPaymentMethod> _paymentMethodRepository = Substitute.For<IRepository<SavedPaymentMethod>>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly PaymentService _sut;

    public FulfilAsyncTests()
    {
        _sut = new PaymentService(_paymentRepository, _paymentMethodRepository, _gateway);
    }

    private Payment AuthorizedPayment(DateTimeOffset? expiresAt = null)
    {
        var payment = new Payment(1, "buyer@example.com", 8.50m, "USD", "ESHOP-1-abc");
        payment.MarkAuthorized("AUTH-1", "CREATED", 8.50m, expiresAt ?? DateTimeOffset.UtcNow.AddDays(3));
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>()).Returns(payment);
        return payment;
    }

    [Fact]
    public async Task CaptureReadsFeeAndNetFromTheGateway()
    {
        AuthorizedPayment();
        _gateway.CaptureAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CaptureResult { CaptureId = "CAP-1", Status = "COMPLETED", CapturedAmount = 8.50m, PayPalFee = 0.71m, NetAmount = 7.79m });

        var result = await _sut.FulfilAsync(1);

        Assert.Equal("Fulfilled", result!.Status);
        Assert.Equal("CAP-1", result.CaptureId);
        Assert.Equal(8.50m, result.CapturedAmount);
        Assert.Equal(0.71m, result.PayPalFee);
        Assert.Equal(7.79m, result.NetAmount);
    }

    [Fact]
    public async Task ADoubleClickDoesNotCaptureTwice()
    {
        AuthorizedPayment();
        _gateway.CaptureAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CaptureResult { CaptureId = "CAP-1", Status = "COMPLETED", CapturedAmount = 8.50m });

        await _sut.FulfilAsync(1);
        await _sut.FulfilAsync(1);

        await _gateway.Received(1).CaptureAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FulfillingAnUnauthorizedOrderThrows()
    {
        var payment = new Payment(1, "buyer@example.com", 8.50m, "USD", "ESHOP-1-abc");
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>()).Returns(payment);

        await Assert.ThrowsAsync<PaymentStateConflictException>(() => _sut.FulfilAsync(1));
    }

    [Fact]
    public async Task AStaleAuthorizationIsRenewedBeforeCapture()
    {
        AuthorizedPayment(expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        _gateway.ReauthorizeAsync("AUTH-1", 8.50m, "USD", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReauthorizationResult { AuthorizationId = "AUTH-1", Status = "CREATED", ExpiresAt = DateTimeOffset.UtcNow.AddDays(3) });
        _gateway.CaptureAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new CaptureResult { CaptureId = "CAP-1", Status = "COMPLETED", CapturedAmount = 8.50m });

        var result = await _sut.FulfilAsync(1);

        Assert.Equal("Fulfilled", result!.Status);
        await _gateway.Received(1).ReauthorizeAsync("AUTH-1", 8.50m, "USD", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNullWhenNoSuchOrderExists()
    {
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>()).Returns((Payment?)null);

        var result = await _sut.FulfilAsync(999);

        Assert.Null(result);
    }
}
