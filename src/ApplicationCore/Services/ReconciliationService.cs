using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IPayPalReportingClient _reportingClient;
    private readonly IRepository<Order> _orderRepository;

    public ReconciliationService(IPayPalReportingClient reportingClient, IRepository<Order> orderRepository)
    {
        _reportingClient = reportingClient;
        _orderRepository = orderRepository;
    }

    public async Task<ReconciliationReport> BuildReportAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
            throw new ArgumentException("'to' must not be earlier than 'from'.", nameof(to));

        // PayPal's own record for the whole range (the client splits it into <=31-day windows).
        var transactions = await _reportingClient.SearchTransactionsAsync(from, to, cancellationToken);

        // eShop's own payments.
        var orders = await _orderRepository.ListAsync(new OrdersWithPaymentSpecification(), cancellationToken);
        var paymentsInRange = orders
            .Where(o => o.Payment is not null && InRange(o, from, to))
            .ToList();

        // Index every PayPal identifier eShop knows about back to its order.
        var byReference = new Dictionary<string, Order>(StringComparer.OrdinalIgnoreCase);
        foreach (var order in orders)
        {
            var p = order.Payment!;
            AddKey(byReference, p.PayPalOrderId, order);
            AddKey(byReference, p.AuthorizationId, order);
            AddKey(byReference, p.CaptureId, order);
            AddKey(byReference, order.Id.ToString(), order); // custom_id / invoice_id we set to the order id
            foreach (var refund in p.Refunds)
                AddKey(byReference, refund.PayPalRefundId, order);
        }

        var matched = new List<ReconciliationMatch>();
        var payPalOnly = new List<PayPalOnlyRecord>();
        var matchedOrderIds = new HashSet<int>();

        foreach (var txn in transactions)
        {
            var order = MatchOrder(txn, byReference);
            if (order is not null)
            {
                matchedOrderIds.Add(order.Id);
                matched.Add(new ReconciliationMatch(
                    order.Id, txn.TransactionId, txn.ReferenceId, txn.EventCode, txn.Status,
                    txn.Amount, txn.CurrencyCode));
            }
            else
            {
                payPalOnly.Add(new PayPalOnlyRecord(
                    txn.TransactionId, txn.ReferenceId, txn.EventCode, txn.Status, txn.Amount,
                    txn.CurrencyCode, txn.InvoiceId, txn.CustomField));
            }
        }

        var eShopOnly = paymentsInRange
            .Where(o => !matchedOrderIds.Contains(o.Id))
            .Select(o => new EShopOnlyRecord(
                o.Id, o.Payment!.PayPalOrderId, o.Payment.AuthorizationId, o.Payment.CaptureId,
                o.Status.ToString(),
                // The captured amount if taken, otherwise the amount currently held.
                o.Payment.CapturedAmount ?? o.Payment.AuthorizedAmount, o.Payment.Currency))
            .ToList();

        return new ReconciliationReport(from, to, matched, payPalOnly, eShopOnly);
    }

    private static bool InRange(Order order, DateTimeOffset from, DateTimeOffset to)
    {
        if (order.OrderDate >= from && order.OrderDate <= to) return true;
        var capturedAt = order.Payment?.CapturedAt;
        return capturedAt.HasValue && capturedAt.Value >= from && capturedAt.Value <= to;
    }

    private static void AddKey(IDictionary<string, Order> map, string? key, Order order)
    {
        if (!string.IsNullOrEmpty(key)) map[key!] = order;
    }

    private static Order? MatchOrder(PayPalTransaction txn, IDictionary<string, Order> byReference)
    {
        foreach (var candidate in new[] { txn.ReferenceId, txn.TransactionId, txn.CustomField, txn.InvoiceId })
        {
            if (!string.IsNullOrEmpty(candidate) && byReference.TryGetValue(candidate!, out var order))
                return order;
        }
        return null;
    }
}
