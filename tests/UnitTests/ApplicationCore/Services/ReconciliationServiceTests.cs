using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services;

public class ReconciliationServiceTests
{
    private readonly IPayPalTransactionSearchClient _search = Substitute.For<IPayPalTransactionSearchClient>();
    private readonly IReadRepository<Order> _orders = Substitute.For<IReadRepository<Order>>();

    private static int _nextId = 1;

    private static Order FulfilledOrder(string payPalOrderId, string invoiceId)
    {
        var items = new List<OrderItem> { new OrderItem(new CatalogItemOrdered(1, "Item", "pic.png"), 20m, 1) };
        var order = new Order("buyer@x.com", new Address("s", "c", "st", "US", "12345"), items);
        typeof(Order).GetProperty("Id")!.SetValue(order, _nextId++);
        var payment = new OrderPayment(payPalOrderId, "USD", 20m, invoiceId);
        payment.RecordAuthorization("AUTH", "CREATED", DateTimeOffset.UtcNow);
        payment.RecordCapture("CAP", "COMPLETED", 20m, 1m, 19m);
        order.BeginPayment(payment);
        order.MarkFulfilled();
        return order;
    }

    [Fact]
    public async Task Buckets_Matched_PayPalOnly_And_EShopOnly()
    {
        var matchedByInvoice = FulfilledOrder("PPO-A", "eshop-order-1-runA");
        var matchedByRef = FulfilledOrder("PPO-B", "eshop-order-2-runB");
        var unmatchedLocal = FulfilledOrder("PPO-C", "eshop-order-3-runC"); // no matching txn -> eShop-only

        _orders.ListAsync(Arg.Any<OrdersWithPaymentSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new List<Order> { matchedByInvoice, matchedByRef, unmatchedLocal });

        _search.SearchAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<PayPalTransactionRecord>
            {
                new() { TransactionId = "T1", InvoiceId = "eshop-order-1-runA", Amount = 20m, CurrencyCode = "USD", Status = "S" },
                new() { TransactionId = "T2", PayPalReferenceId = "PPO-B", PayPalReferenceIdType = "ODR", Amount = 20m, CurrencyCode = "USD" },
                new() { TransactionId = "T3", InvoiceId = "totally-unrelated", PayPalReferenceId = "OTHER", Amount = 99m, CurrencyCode = "USD" }
            });

        var svc = new ReconciliationService(_search, _orders);
        var report = await svc.ReconcileAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        Assert.Equal(3, report.PayPalTransactionCount);
        Assert.Equal(2, report.Matched.Count);
        Assert.Single(report.PayPalOnly);
        Assert.Equal("T3", report.PayPalOnly[0].TransactionId);
        Assert.Single(report.EShopOnly);
        Assert.Equal("PPO-C", report.EShopOnly[0].PayPalOrderId);
    }

    [Fact]
    public async Task BareOrderId_DoesNotProduceFalseMatch()
    {
        // A local order whose invoice_id is "eshop-order-1-runX"; a PayPal txn from a different run
        // carries "eshop-order-1-runY". They share the bare id but must NOT match.
        var order = FulfilledOrder("PPO-A", "eshop-order-1-runX");
        _orders.ListAsync(Arg.Any<OrdersWithPaymentSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new List<Order> { order });
        _search.SearchAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<PayPalTransactionRecord>
            {
                new() { TransactionId = "T1", InvoiceId = "eshop-order-1-runY", CustomField = "1", Amount = 20m }
            });

        var svc = new ReconciliationService(_search, _orders);
        var report = await svc.ReconcileAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        Assert.Empty(report.Matched);
        Assert.Single(report.PayPalOnly);
        Assert.Single(report.EShopOnly);
    }
}
