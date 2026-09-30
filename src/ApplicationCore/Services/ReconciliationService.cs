using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ReconciliationService : IReconciliationService
{
    // PayPal Transaction Search v1 hard limits: <=31 days per call, <=500 per page.
    private const int MaxWindowDays = 31;
    private const int PageSize = 500;

    private readonly IPayPalClient _payPalClient;
    private readonly IReadRepository<Order> _orderRepository;

    public ReconciliationService(IPayPalClient payPalClient, IReadRepository<Order> orderRepository)
    {
        _payPalClient = payPalClient;
        _orderRepository = orderRepository;
    }

    public async Task<ReconciliationReport> BuildReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var transactions = new List<PayPalTransactionRecord>();
        foreach (var (windowStart, windowEnd) in SliceWindows(from, to))
        {
            var page = 1;
            var totalPages = 1;
            do
            {
                var pageResult = await _payPalClient.ListTransactionsAsync(windowStart, windowEnd, page, PageSize, ct);
                transactions.AddRange(pageResult.Transactions);
                totalPages = Math.Max(pageResult.TotalPages, 1);
                page++;
            } while (page <= totalPages);
        }

        var orders = await _orderRepository.ListAsync(new OrdersWithPaymentSpecification(), ct);

        var ours = new Dictionary<string, (ReconciledTransactionKind Kind, int OrderId, decimal Amount, DateTimeOffset At)>();
        foreach (var order in orders)
        {
            var payment = order.Payment!;
            if (payment.CaptureId is not null && payment.CapturedAmount is not null)
            {
                ours[payment.CaptureId] = (ReconciledTransactionKind.Capture, order.Id, payment.CapturedAmount.Value,
                    payment.CapturedAt ?? order.OrderDate);
            }
            foreach (var refund in payment.Refunds)
            {
                ours[refund.RefundId] = (ReconciledTransactionKind.Refund, order.Id, refund.Amount, refund.CreatedAt);
            }
        }

        var matched = new List<MatchedTransaction>();
        var inPayPalNotEShop = new List<UnmatchedPayPalTransaction>();
        var matchedIds = new HashSet<string>();

        foreach (var txn in transactions)
        {
            if (ours.TryGetValue(txn.TransactionId, out var ourRecord))
            {
                matched.Add(new MatchedTransaction(txn.TransactionId, ourRecord.Kind, ourRecord.OrderId,
                    txn.Amount, ourRecord.Amount, txn.Status));
                matchedIds.Add(txn.TransactionId);
            }
            else
            {
                inPayPalNotEShop.Add(new UnmatchedPayPalTransaction(txn.TransactionId, txn.Status, txn.Amount,
                    txn.CurrencyCode, txn.InitiatedDate, txn.InvoiceId, txn.CustomField));
            }
        }

        // Only flag an eShop-side transaction as "missing from PayPal" if it actually happened
        // inside the requested range - PayPal's own reporting lag otherwise makes very recent
        // captures/refunds legitimately absent from the report, which is expected, not a gap.
        var inEShopNotPayPal = ours
            .Where(kv => !matchedIds.Contains(kv.Key) && kv.Value.At >= from && kv.Value.At <= to)
            .Select(kv => new UnmatchedEShopTransaction(kv.Key, kv.Value.Kind, kv.Value.OrderId, kv.Value.Amount))
            .ToList();

        return new ReconciliationReport(from, to, matched, inPayPalNotEShop, inEShopNotPayPal);
    }

    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> SliceWindows(DateTimeOffset from, DateTimeOffset to)
    {
        var windowStart = from;
        while (windowStart < to)
        {
            var windowEnd = windowStart.AddDays(MaxWindowDays);
            if (windowEnd > to) windowEnd = to;
            yield return (windowStart, windowEnd);
            windowStart = windowEnd;
        }
    }
}
