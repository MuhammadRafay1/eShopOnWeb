using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IPaymentGateway _gateway;
    private readonly IRepository<OrderPayment> _paymentRepository;
    private readonly IAppLogger<ReconciliationService> _logger;

    public ReconciliationService(
        IPaymentGateway gateway,
        IRepository<OrderPayment> paymentRepository,
        IAppLogger<ReconciliationService> logger)
    {
        _gateway = gateway;
        _paymentRepository = paymentRepository;
        _logger = logger;
    }

    public async Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var search = await _gateway.SearchTransactionsAsync(from, to, cancellationToken);
        var payments = await _paymentRepository.ListAsync(cancellationToken);

        // Index eShop payments by their unique invoice id and by order id for matching.
        var byInvoice = payments
            .Where(p => !string.IsNullOrEmpty(p.InvoiceId))
            .GroupBy(p => p.InvoiceId)
            .ToDictionary(g => g.Key, g => g.First());
        var byOrderId = payments.ToDictionary(p => p.OrderId);

        var lines = new List<ReconciliationLine>();
        var matchedPaymentIds = new HashSet<int>();

        foreach (var txn in search.Transactions)
        {
            OrderPayment? match = null;
            if (!string.IsNullOrEmpty(txn.InvoiceId) && byInvoice.TryGetValue(txn.InvoiceId!, out var byInv))
            {
                match = byInv;
            }
            else if (!string.IsNullOrEmpty(txn.CustomField) && int.TryParse(txn.CustomField, out var orderId) && byOrderId.TryGetValue(orderId, out var byOrd))
            {
                match = byOrd;
            }

            if (match is not null)
            {
                matchedPaymentIds.Add(match.Id);
            }

            lines.Add(new ReconciliationLine
            {
                Match = match is not null ? ReconciliationMatch.Matched : ReconciliationMatch.PayPalOnly,
                PayPalTransactionId = txn.TransactionId,
                PayPalStatus = txn.Status,
                PayPalAmount = txn.Amount,
                PayPalFee = txn.Fee,
                CurrencyCode = txn.CurrencyCode,
                InvoiceId = txn.InvoiceId,
                EventCode = txn.EventCode,
                TransactionDate = txn.InitiationDate,
                OrderId = match?.OrderId,
                EShopPaymentStatus = match?.Status.ToString(),
                EShopAmount = match?.Amount
            });
        }

        // eShop captures whose money has moved within the range but that PayPal's report does not list.
        foreach (var payment in payments)
        {
            if (payment.CaptureId is null || matchedPaymentIds.Contains(payment.Id))
            {
                continue;
            }
            if (payment.UpdatedAt < from || payment.UpdatedAt > to)
            {
                continue;
            }

            lines.Add(new ReconciliationLine
            {
                Match = ReconciliationMatch.EShopOnly,
                CurrencyCode = payment.CurrencyCode,
                InvoiceId = payment.InvoiceId,
                TransactionDate = payment.UpdatedAt,
                OrderId = payment.OrderId,
                EShopPaymentStatus = payment.Status.ToString(),
                EShopAmount = payment.CapturedAmount ?? payment.Amount
            });
        }

        var report = new ReconciliationReport
        {
            From = from,
            To = to,
            Complete = search.Complete,
            PayPalTransactionCount = search.Transactions.Count,
            MatchedCount = lines.Count(l => l.Match == ReconciliationMatch.Matched),
            PayPalOnlyCount = lines.Count(l => l.Match == ReconciliationMatch.PayPalOnly),
            EShopOnlyCount = lines.Count(l => l.Match == ReconciliationMatch.EShopOnly),
            Lines = lines
        };

        _logger.LogInformation(
            $"Reconciliation {from:o}..{to:o}: {report.PayPalTransactionCount} PayPal txns, " +
            $"{report.MatchedCount} matched, {report.PayPalOnlyCount} PayPal-only, {report.EShopOnlyCount} eShop-only (complete={report.Complete}).");

        return report;
    }
}
