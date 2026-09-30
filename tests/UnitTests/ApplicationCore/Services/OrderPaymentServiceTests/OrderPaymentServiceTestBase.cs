using System;
using System.Collections.Generic;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentMethodAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.UnitTests.Builders;
using NSubstitute;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.OrderPaymentServiceTests;

public abstract class OrderPaymentServiceTestBase
{
    protected const string BuyerId = "12345"; // matches OrderBuilder.TestBuyerId
    protected const string OtherBuyerId = "other-shopper";

    protected readonly IPayPalPaymentGateway Gateway = Substitute.For<IPayPalPaymentGateway>();
    protected readonly IRepository<Order> OrderRepo = Substitute.For<IRepository<Order>>();
    protected readonly IRepository<OrderPayment> PaymentRepo = Substitute.For<IRepository<OrderPayment>>();
    protected readonly IRepository<SavedPaymentMethod> CardRepo = Substitute.For<IRepository<SavedPaymentMethod>>();
    protected readonly IAppLogger<OrderPaymentService> Logger = Substitute.For<IAppLogger<OrderPaymentService>>();

    protected OrderPaymentService CreateService() =>
        new OrderPaymentService(Gateway, OrderRepo, PaymentRepo, CardRepo, Logger);

    protected static Order NewOrder() => new OrderBuilder().WithDefaultValues();

    protected static CardDetails SampleCard() => new(
        "4111111111111111", 12, 2030, "123", "Test Shopper",
        "1 Test St", null, "Redmond", "WA", "98052", "US");

    protected static AuthorizationResult AuthResult(decimal amount) => new(
        "PPORDER1", "AUTH1", "COMPLETED", amount, DateTimeOffset.UtcNow.AddDays(29), false);

    protected const int OrderId = 1;

    protected static OrderPayment PaymentFor(decimal amount, DateTimeOffset? expiresAt = null) =>
        new OrderPayment(
            orderId: OrderId,
            currency: "USD",
            payPalOrderId: "PPORDER1",
            payPalAuthorizationId: "AUTH1",
            authorizedAmount: amount,
            authorizationExpiresAt: expiresAt ?? DateTimeOffset.UtcNow.AddDays(29),
            savedPaymentMethodId: null);
}
