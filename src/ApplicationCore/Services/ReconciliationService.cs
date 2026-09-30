using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IPayPalGateway _payPal;

    public ReconciliationService(IRepository<Order> orderRepository, IPayPalGateway payPal)
    {
        _orderRepository = orderRepository;
        _payPal = payPal;
    }

    public async Task<ReconciliationReport> BuildReportAsync(DateTimeOffset from, DateTimeOffset to)
    {
        // PayPal's own record of transactions for the range - our source of truth on PayPal's side.
        var transactions = await _payPal.SearchTransactionsAsync(from, to);

        // eShop's own record of money movement (captures/refunds) that fell in the same range. The
        // custom_id set on the purchase unit at authorize time (== the eShop order id) survives into
        // PayPal's reporting as custom_field, which is what lines the two sides up.
        var allOrders = await _orderRepository.ListAsync();
        var ordersWithActivityInRange = allOrders.Where(o =>
                (o.Payment?.CapturedAt is DateTimeOffset capturedAt && capturedAt >= from && capturedAt <= to) ||
                (o.Payment?.Refunds.Any(r => r.CreatedAt >= from && r.CreatedAt <= to) ?? false))
            .ToList();

        var transactionsByOrderId = transactions
            .Where(t => t.CustomField is not null)
            .ToLookup(t => t.CustomField!);

        var matchedTransactionIds = new HashSet<string>();
        var entries = new List<ReconciliationEntry>();

        foreach (var order in ordersWithActivityInRange)
        {
            var matches = transactionsByOrderId[order.Id.ToString()].ToList();

            if (matches.Count == 0)
            {
                entries.Add(new ReconciliationEntry(
                    ReconciliationMatchState.InEShopNotInPayPal,
                    order.Id,
                    null,
                    order.Payment?.CapturedAmount,
                    order.Payment?.Currency,
                    order.Status.ToString(),
                    order.Payment?.CapturedAt,
                    "eShop recorded payment activity for this order, but PayPal's transaction report doesn't show it yet for this range. " +
                    "PayPal's reporting typically lags live activity by a few hours - expected for very recent activity, not necessarily a gap."));
                continue;
            }

            foreach (var transaction in matches)
            {
                matchedTransactionIds.Add(transaction.TransactionId);
                entries.Add(new ReconciliationEntry(
                    ReconciliationMatchState.Matched,
                    order.Id,
                    transaction.TransactionId,
                    transaction.Amount,
                    transaction.Currency,
                    transaction.Status,
                    transaction.TransactionDate,
                    "Matched by eShop order id via PayPal's custom_field."));
            }
        }

        foreach (var transaction in transactions)
        {
            if (matchedTransactionIds.Contains(transaction.TransactionId)) continue;

            int? eshopOrderId = transaction.CustomField is not null && int.TryParse(transaction.CustomField, out var parsedId) ? parsedId : null;
            entries.Add(new ReconciliationEntry(
                ReconciliationMatchState.InPayPalNotInEShop,
                eshopOrderId,
                transaction.TransactionId,
                transaction.Amount,
                transaction.Currency,
                transaction.Status,
                transaction.TransactionDate,
                "PayPal has a transaction in this range with no corresponding eShop order payment activity in the same range."));
        }

        return new ReconciliationReport(from, to, entries);
    }
}
