using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Lines up PayPal's own transaction record for a date range against eShop's captures and refunds, surfacing
/// rows present on only one side in both directions.
/// </summary>
public sealed class ReconciliationService : IReconciliationService
{
    private readonly ITransactionReportReader _reader;
    private readonly IReadRepository<Order> _orderRepository;

    public ReconciliationService(ITransactionReportReader reader, IReadRepository<Order> orderRepository)
    {
        _reader = reader;
        _orderRepository = orderRepository;
    }

    public async Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var payPal = await _reader.SearchAsync(from, to, cancellationToken);

        // Build eShop's own money-moving transactions that fall in the range.
        var orders = await _orderRepository.ListAsync(new PaidOrdersSpecification(), cancellationToken);
        var eShop = BuildEShopTransactions(orders, from, to);

        var payPalById = payPal.Records
            .Where(r => !string.IsNullOrEmpty(r.TransactionId))
            .GroupBy(r => r.TransactionId!)
            .ToDictionary(g => g.Key, g => g.First());
        var eShopById = eShop.ToDictionary(e => e.TransactionId, StringComparer.Ordinal);

        var matched = new List<ReconciliationMatch>();
        var payPalOnly = new List<ReconciliationPayPalOnly>();
        var eShopOnly = new List<ReconciliationEShopOnly>();

        foreach (var record in payPal.Records)
        {
            if (record.TransactionId is not null && eShopById.TryGetValue(record.TransactionId, out var e))
            {
                matched.Add(new ReconciliationMatch(
                    record.TransactionId, e.Kind, record.Amount, e.Amount, record.Status, e.OrderId));
            }
            else
            {
                payPalOnly.Add(new ReconciliationPayPalOnly(
                    record.TransactionId, record.Amount, record.CurrencyCode, record.Status, record.InitiationDate));
            }
        }

        foreach (var e in eShop)
        {
            if (!payPalById.ContainsKey(e.TransactionId))
            {
                eShopOnly.Add(new ReconciliationEShopOnly(e.TransactionId, e.Kind, e.Amount, e.OrderId));
            }
        }

        return new ReconciliationReport(from, to, matched, payPalOnly, eShopOnly, payPal.Truncated, payPal.TruncatedAfter);
    }

    private static List<EShopTxn> BuildEShopTransactions(IEnumerable<Order> orders, DateTimeOffset from, DateTimeOffset to)
    {
        var result = new List<EShopTxn>();
        foreach (var order in orders)
        {
            var payment = order.Payment;
            if (payment is null)
            {
                continue;
            }

            if (payment.PayPalCaptureId is not null)
            {
                var captureTime = order.FulfilledAt ?? payment.UpdatedAt;
                if (InRange(captureTime, from, to))
                {
                    result.Add(new EShopTxn(payment.PayPalCaptureId, "CAPTURE", payment.CapturedAmount ?? payment.Amount, order.Id));
                }
            }

            foreach (var refund in payment.Refunds)
            {
                if (refund.PayPalRefundId is not null && InRange(refund.CreatedAt, from, to))
                {
                    result.Add(new EShopTxn(refund.PayPalRefundId, "REFUND", refund.Amount, order.Id));
                }
            }
        }
        return result;
    }

    private static bool InRange(DateTimeOffset value, DateTimeOffset from, DateTimeOffset to) =>
        value >= from && value <= to;

    private sealed record EShopTxn(string TransactionId, string Kind, decimal Amount, int OrderId);
}
