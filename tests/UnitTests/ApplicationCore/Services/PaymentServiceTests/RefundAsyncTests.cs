using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.UnitTests.ApplicationCore.Helpers;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.PaymentServiceTests;

public class RefundAsyncTests
{
    private readonly IRepository<Payment> _paymentRepository = Substitute.For<IRepository<Payment>>();
    private readonly IRepository<SavedPaymentMethod> _paymentMethodRepository = Substitute.For<IRepository<SavedPaymentMethod>>();
    private readonly IPaymentGateway _gateway = Substitute.For<IPaymentGateway>();
    private readonly PaymentService _sut;

    public RefundAsyncTests()
    {
        _sut = new PaymentService(_paymentRepository, _paymentMethodRepository, _gateway);
    }

    private Payment CapturedPayment(decimal amount = 8.50m)
    {
        var payment = new Payment(1, "buyer@example.com", amount, "USD", "ESHOP-1-abc");
        EntityIdHelper.SetId(payment, 1);
        payment.MarkAuthorized("AUTH-1", "CREATED", amount, null);
        payment.MarkCaptured("CAP-1", "COMPLETED", amount, 0.71m, amount - 0.71m);
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>()).Returns(payment);
        return payment;
    }

    [Fact]
    public async Task RepeatingTheSameIdempotencyKeyNeverRefundsTwice()
    {
        CapturedPayment();
        _gateway.RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new RefundResult { RefundId = "REF-1", Status = "COMPLETED", Amount = 1.00m });

        var first = await _sut.RefundAsync(1, "buyer@example.com", "key-1", 1.00m);
        var second = await _sut.RefundAsync(1, "buyer@example.com", "key-1", 1.00m);

        Assert.Equal("REF-1", first!.RefundId);
        Assert.Equal("REF-1", second!.RefundId);
        await _gateway.Received(1).RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TwoDistinctIdempotencyKeysProduceTwoLegitimateRefunds()
    {
        CapturedPayment();
        _gateway.RefundAsync("CAP-1", 1.00m, "USD", "key-1", Arg.Any<CancellationToken>())
            .Returns(new RefundResult { RefundId = "REF-1", Status = "COMPLETED", Amount = 1.00m });
        _gateway.RefundAsync("CAP-1", 2.00m, "USD", "key-2", Arg.Any<CancellationToken>())
            .Returns(new RefundResult { RefundId = "REF-2", Status = "COMPLETED", Amount = 2.00m });

        var first = await _sut.RefundAsync(1, "buyer@example.com", "key-1", 1.00m);
        var second = await _sut.RefundAsync(1, "buyer@example.com", "key-2", 2.00m);

        Assert.Equal("REF-1", first!.RefundId);
        Assert.Equal("REF-2", second!.RefundId);
        Assert.Equal(3.00m, second.TotalRefunded);
    }

    [Fact]
    public async Task AnOverRefundIsRejectedWithoutCallingTheGateway()
    {
        var payment = CapturedPayment(8.50m);
        payment.AddRefund("REF-1", 5.00m, "COMPLETED", "already-refunded-key");

        await Assert.ThrowsAsync<PaymentRejectedException>(
            () => _sut.RefundAsync(1, "buyer@example.com", "new-key", 5.00m));

        await _gateway.DidNotReceive().RefundAsync(Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<string>(), "new-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsNullWhenTheOrderBelongsToAnotherBuyer()
    {
        CapturedPayment();

        var result = await _sut.RefundAsync(1, "someone-else@example.com", "key-1", 1.00m);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefundingBeforeCaptureThrows()
    {
        var payment = new Payment(1, "buyer@example.com", 8.50m, "USD", "ESHOP-1-abc");
        payment.MarkAuthorized("AUTH-1", "CREATED", 8.50m, null);
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Payment>>(), Arg.Any<CancellationToken>()).Returns(payment);

        await Assert.ThrowsAsync<PaymentStateConflictException>(
            () => _sut.RefundAsync(1, "buyer@example.com", "key-1", 1.00m));
    }
}
