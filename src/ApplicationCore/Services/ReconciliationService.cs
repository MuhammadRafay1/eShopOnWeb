using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IPayPalTransactionSearchClient _searchClient;
    private readonly IReadRepository<Order> _orderRepository;

    public ReconciliationService(
        IPayPalTransactionSearchClient searchClient,
        IReadRepository<Order> orderRepository)
    {
        _searchClient = searchClient;
        _orderRepository = orderRepository;
    }

    public async Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var transactions = await _searchClient.SearchAsync(from, to);
        var orders = await _orderRepository.ListAsync(new OrdersWithPaymentSpecification());

        // Index local orders by the two globally-unique correlation keys set at order time: the exact
        // invoice_id and the PayPal order id. Bare order ids are deliberately NOT used as a key — they
        // repeat across runs/deployments and would produce false matches against unrelated PayPal
        // transactions.
        var ordersByPayPalOrderId = new Dictionary<string, Order>(StringComparer.OrdinalIgnoreCase);
        var ordersByInvoiceId = new Dictionary<string, Order>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in orders)
        {
            if (o.Payment?.PayPalOrderId is { Length: > 0 } ppid)
            {
                ordersByPayPalOrderId[ppid] = o;
            }
            if (o.Payment?.InvoiceId is { Length: > 0 } inv)
            {
                ordersByInvoiceId[inv] = o;
            }
        }

        var report = new ReconciliationReport
        {
            From = from,
            To = to,
            PayPalTransactionCount = transactions.Count
        };

        var matchedOrderIds = new HashSet<int>();

        foreach (var txn in transactions)
        {
            var order = MatchOrder(txn, ordersByPayPalOrderId, ordersByInvoiceId);
            if (order is null)
            {
                report.PayPalOnly.Add(txn);
                continue;
            }

            matchedOrderIds.Add(order.Id);
            report.Matched.Add(new ReconciliationMatch
            {
                OrderId = order.Id,
                OrderStatus = order.Status.ToString(),
                PayPalOrderId = order.Payment?.PayPalOrderId,
                PayPalTransactionId = txn.TransactionId,
                TransactionStatus = txn.Status,
                PayPalAmount = txn.Amount,
                CurrencyCode = txn.CurrencyCode
            });
        }

        // Local orders whose money moved (fulfilled or refunded) but which no PayPal transaction in
        // range matched — the "eShop knows, PayPal doesn't (yet)" anomaly.
        foreach (var o in orders)
        {
            var moneyMoved = o.Status is OrderStatus.Fulfilled or OrderStatus.PartiallyRefunded or OrderStatus.Refunded;
            if (moneyMoved && !matchedOrderIds.Contains(o.Id))
            {
                report.EShopOnly.Add(new ReconciliationLocalOrder
                {
                    OrderId = o.Id,
                    OrderStatus = o.Status.ToString(),
                    PayPalOrderId = o.Payment?.PayPalOrderId,
                    CaptureId = o.Payment?.PayPalCaptureId,
                    CapturedAmount = o.Payment?.CapturedAmount
                });
            }
        }

        return report;
    }

    private static Order? MatchOrder(
        PayPalTransactionRecord txn,
        IReadOnlyDictionary<string, Order> ordersByPayPalOrderId,
        IReadOnlyDictionary<string, Order> ordersByInvoiceId)
    {
        // Primary key: exact invoice_id (globally unique — carries the order id and the run token).
        if (!string.IsNullOrEmpty(txn.InvoiceId) && ordersByInvoiceId.TryGetValue(txn.InvoiceId!, out var byInvoice))
        {
            return byInvoice;
        }

        // Secondary key: paypal_reference_id equals the stored PayPal order id (type ODR).
        if (!string.IsNullOrEmpty(txn.PayPalReferenceId)
            && (txn.PayPalReferenceIdType is null or "ODR")
            && ordersByPayPalOrderId.TryGetValue(txn.PayPalReferenceId!, out var byRef))
        {
            return byRef;
        }

        return null;
    }
}
