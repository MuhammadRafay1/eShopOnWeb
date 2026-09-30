using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PaymentGateway;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Builds a reconciliation report over a date range: pulls PayPal's own transaction records (chunked into
/// ≤31-day windows, paged to exhaustion by the gateway) and lines them up against local eShop payments,
/// surfacing PayPal-only and local-only discrepancies. Correlates by PayPal id equality first, then by the
/// eShop order id stamped into the transaction's invoice id.
/// </summary>
public class ReconciliationService : IReconciliationService
{
    // PayPal's transaction search caps a single call at 31 days; window defensively below that.
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(31);

    // OrderPayment.UpdatedAt only approximates when PayPal recorded the movement, so widen the local load.
    private static readonly TimeSpan LocalBuffer = TimeSpan.FromDays(2);

    private readonly IPayPalPaymentGateway _gateway;
    private readonly IReadRepository<OrderPayment> _paymentRepository;
    private readonly IAppLogger<ReconciliationService> _logger;

    public ReconciliationService(
        IPayPalPaymentGateway gateway,
        IReadRepository<OrderPayment> paymentRepository,
        IAppLogger<ReconciliationService> logger)
    {
        _gateway = gateway;
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    public async Task<ReconciliationReport> BuildReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (from > to)
        {
            throw new InvalidPaymentRequestException("'from' must be earlier than or equal to 'to'.");
        }

        // (2) Chunk the range and pull every PayPal transaction across every window.
        var payPalTransactions = new List<PayPalTransactionRecord>();
        foreach (var (chunkFrom, chunkTo) in ChunkRange(from, to))
        {
            var records = await _gateway.SearchTransactionsAsync(chunkFrom, chunkTo, ct);
            payPalTransactions.AddRange(records);
        }

        // (3) Load local payments that could plausibly fall in range (+ buffer), with their refunds.
        var localPayments = await _paymentRepository.ListAsync(
            new OrderPaymentsWithRefundsSpecification(from - LocalBuffer, to + LocalBuffer), ct);

        // Balance-affecting local movements only (captures + refunds) — PayPal's search returns those, not
        // holds/authorizations, so matching against authorization ids would create false discrepancies.
        var localRefs = new List<LocalReference>();
        var customFieldToOrderId = new Dictionary<string, int>();
        foreach (var payment in localPayments)
        {
            customFieldToOrderId[payment.OrderId.ToString(CultureInfo.InvariantCulture)] = payment.OrderId;
            if (payment.PayPalCaptureId is not null)
            {
                localRefs.Add(new LocalReference(payment.OrderId, payment.PayPalCaptureId, "capture", payment.CapturedAmount));
            }
            foreach (var refund in payment.Refunds)
            {
                localRefs.Add(new LocalReference(payment.OrderId, refund.PayPalRefundId, "refund", refund.Amount));
            }
        }
        var refById = localRefs
            .GroupBy(r => r.PayPalReference)
            .ToDictionary(g => g.Key, g => g.First());

        var matched = new List<ReconciliationMatch>();
        var payPalOnly = new List<PayPalOnlyRecord>();
        var matchedRefIds = new HashSet<string>();
        var matchedOrderIds = new HashSet<int>();

        foreach (var txn in payPalTransactions)
        {
            if (refById.TryGetValue(txn.TransactionId, out var byId))
            {
                matched.Add(new ReconciliationMatch(
                    txn.TransactionId, txn.Amount, txn.StatusRaw, byId.OrderId, byId.PayPalReference, byId.Kind));
                matchedRefIds.Add(byId.PayPalReference);
                matchedOrderIds.Add(byId.OrderId);
            }
            else if (txn.CustomField is not null && customFieldToOrderId.TryGetValue(txn.CustomField, out var orderId))
            {
                matched.Add(new ReconciliationMatch(
                    txn.TransactionId, txn.Amount, txn.StatusRaw, orderId, txn.CustomField, "custom-field"));
                matchedOrderIds.Add(orderId);
            }
            else
            {
                payPalOnly.Add(new PayPalOnlyRecord(
                    txn.TransactionId, txn.Amount, txn.CurrencyCode, txn.StatusRaw, txn.InitiatedAt, txn.InvoiceId));
            }
        }

        var localOnly = localRefs
            .Where(r => !matchedRefIds.Contains(r.PayPalReference) && !matchedOrderIds.Contains(r.OrderId))
            .Select(r => new LocalOnlyRecord(r.OrderId, r.PayPalReference, r.Kind, r.Amount))
            .ToList();

        var summary = new ReconciliationSummary(
            payPalTransactions.Count, localRefs.Count, matched.Count, payPalOnly.Count, localOnly.Count);

        _logger.LogInformation(
            "Reconciliation {0}..{1}: {2} PayPal txns, {3} local refs, {4} matched, {5} PayPal-only, {6} local-only.",
            from, to, payPalTransactions.Count, localRefs.Count, matched.Count, payPalOnly.Count, localOnly.Count);

        return new ReconciliationReport(from, to, matched, payPalOnly, localOnly, summary);
    }

    private static IEnumerable<(DateTimeOffset From, DateTimeOffset To)> ChunkRange(DateTimeOffset from, DateTimeOffset to)
    {
        var cursor = from;
        while (cursor < to)
        {
            var chunkEnd = cursor + MaxWindow;
            if (chunkEnd > to) chunkEnd = to;
            yield return (cursor, chunkEnd);
            cursor = chunkEnd;
        }
    }

    private record LocalReference(int OrderId, string PayPalReference, string Kind, decimal? Amount);
}
