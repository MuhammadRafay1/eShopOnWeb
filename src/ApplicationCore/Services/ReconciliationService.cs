using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderPaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ReconciliationService : IReconciliationService
{
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(31);

    private readonly IReadRepository<OrderPayment> _paymentReadRepository;
    private readonly IPayPalPaymentGateway _gateway;

    public ReconciliationService(IReadRepository<OrderPayment> paymentReadRepository, IPayPalPaymentGateway gateway)
    {
        _paymentReadRepository = paymentReadRepository;
        _gateway = gateway;
    }

    public async Task<ReconciliationReport> BuildReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var payPalTransactions = new List<PayPalTransactionRecord>();
        var complete = true;

        foreach (var (windowStart, windowEnd) in ChunkWindows(from, to))
        {
            var result = await _gateway.SearchTransactionsAsync(windowStart, windowEnd, cancellationToken);
            payPalTransactions.AddRange(result.Transactions);
            complete &= result.Complete;
        }

        var orderPayments = await _paymentReadRepository.ListAsync(new OrderPaymentsWithActivityInRangeSpec(from, to), cancellationToken);

        var matched = new List<ReconciliationMatch>();
        var payPalOnly = new List<ReconciliationPayPalSide>();
        var eShopOnly = new List<ReconciliationOrderSide>();
        var matchedPayPalTransactionIds = new HashSet<string>();

        foreach (var payment in orderPayments)
        {
            var orderSide = new ReconciliationOrderSide(payment.OrderId, payment.PayPalOrderId, payment.CaptureId, payment.CapturedGrossAmount, payment.Status.ToString());

            // custom_field is set to the bare order id (stable, exact) - the primary match. invoice_id
            // carries an "ESHOP-{orderId}-" prefix plus a per-pay-attempt nonce (PayPal requires
            // invoice_id to be unique per transaction), so it is matched by prefix as a fallback.
            var invoiceIdPrefix = $"ESHOP-{payment.OrderId}-";
            var orderIdText = payment.OrderId.ToString();
            var match = payPalTransactions.FirstOrDefault(t => t.CustomField == orderIdText)
                ?? payPalTransactions.FirstOrDefault(t => t.InvoiceId is not null && t.InvoiceId.StartsWith(invoiceIdPrefix, StringComparison.Ordinal));

            if (match is not null)
            {
                matchedPayPalTransactionIds.Add(match.TransactionId);
                matched.Add(new ReconciliationMatch(orderSide, ToPayPalSide(match)));
            }
            else
            {
                eShopOnly.Add(orderSide);
            }
        }

        foreach (var transaction in payPalTransactions)
        {
            if (!matchedPayPalTransactionIds.Contains(transaction.TransactionId))
            {
                payPalOnly.Add(ToPayPalSide(transaction));
            }
        }

        return new ReconciliationReport(from, to, matched, payPalOnly, eShopOnly, complete);
    }

    private static ReconciliationPayPalSide ToPayPalSide(PayPalTransactionRecord t) =>
        new(t.TransactionId, t.Amount, t.Status, t.InvoiceId, t.CustomField);

    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> ChunkWindows(DateTimeOffset from, DateTimeOffset to)
    {
        var start = from;
        while (start < to)
        {
            var end = start + MaxWindow < to ? start + MaxWindow : to;
            yield return (start, end);
            start = end;
        }

        if (from == to)
        {
            yield return (from, to);
        }
    }
}
