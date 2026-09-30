using System;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Entities.PaymentAggregateTests;

public class PaymentAuthorizationTests
{
    private static Payment CreatePayment(decimal amount = 8.50m) =>
        new(orderId: 1, buyerId: "buyer@example.com", amount: amount, currencyCode: "USD", correlationReference: "ESHOP-1-abc");

    [Fact]
    public void StartsAwaitingPayment()
    {
        var payment = CreatePayment();
        Assert.Equal(PaymentStatus.AwaitingPayment, payment.Status);
    }

    [Fact]
    public void BeginAuthorizationOnlySetsKeyOnce()
    {
        var payment = CreatePayment();
        payment.BeginAuthorization("first-key");
        payment.BeginAuthorization("second-key");

        Assert.Equal("first-key", payment.PayRequestKey);
    }

    [Fact]
    public void MarkAuthorizedSetsStatusAndFields()
    {
        var payment = CreatePayment();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(3);

        payment.MarkAuthorized("AUTH-1", "CREATED", 8.50m, expiresAt);

        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("AUTH-1", payment.AuthorizationId);
        Assert.Equal("CREATED", payment.AuthorizationStatus);
        Assert.Equal(expiresAt, payment.AuthorizationExpiresAt);
    }

    [Fact]
    public void MarkAuthorizedRejectsAMismatchedHeldAmount()
    {
        var payment = CreatePayment(amount: 8.50m);

        Assert.Throws<PaymentGatewayException>(() => payment.MarkAuthorized("AUTH-1", "CREATED", 9.99m, null));
        Assert.Equal(PaymentStatus.AwaitingPayment, payment.Status);
    }

    [Fact]
    public void MarkVoidedFromAuthorizedSucceeds()
    {
        var payment = CreatePayment();
        payment.MarkAuthorized("AUTH-1", "CREATED", 8.50m, null);

        payment.MarkVoided();

        Assert.Equal(PaymentStatus.Canceled, payment.Status);
    }

    [Fact]
    public void MarkVoidedFromFulfilledThrows()
    {
        var payment = CreatePayment();
        payment.MarkAuthorized("AUTH-1", "CREATED", 8.50m, null);
        payment.MarkCaptured("CAP-1", "COMPLETED", 8.50m, 0.71m, 7.79m);

        Assert.Throws<PaymentStateConflictException>(() => payment.MarkVoided());
    }
}
