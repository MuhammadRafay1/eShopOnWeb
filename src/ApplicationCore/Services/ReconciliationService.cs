using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IPayPalGateway _gateway;
    private readonly IRepository<Payment> _paymentRepository;

    public ReconciliationService(IPayPalGateway gateway, IRepository<Payment> paymentRepository)
    {
        _gateway = gateway;
        _paymentRepository = paymentRepository;
    }

    public async Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var transactions = await _gateway.SearchTransactionsAsync(from, to, ct);
        var localPayments = await _paymentRepository.ListAsync(new PaymentsCapturedInRangeSpec(from, to), ct);

        var transactionsByInvoice = transactions
            .Where(t => !string.IsNullOrEmpty(t.InvoiceId))
            .GroupBy(t => t.InvoiceId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var localByInvoice = localPayments.ToDictionary(p => p.InvoiceReference);

        var matched = new List<MatchedReconciliationEntry>();
        var eShopOnly = new List<EShopOnlyPayment>();

        foreach (var payment in localPayments)
        {
            if (!transactionsByInvoice.TryGetValue(payment.InvoiceReference, out var matchingTxns))
            {
                eShopOnly.Add(new EShopOnlyPayment(payment.OrderId, payment.InvoiceReference, payment.CapturedAmount ?? payment.Amount, payment.Status.ToString()));
                continue;
            }

            var eShopAmount = payment.CapturedAmount ?? payment.Amount;
            foreach (var txn in matchingTxns)
            {
                matched.Add(new MatchedReconciliationEntry(
                    payment.InvoiceReference,
                    payment.OrderId,
                    eShopAmount,
                    txn.Amount,
                    txn.Status,
                    AmountMismatch: txn.Amount != eShopAmount));
            }
        }

        var payPalOnly = transactions
            .Where(t => string.IsNullOrEmpty(t.InvoiceId) || !localByInvoice.ContainsKey(t.InvoiceId))
            .Select(t => new PayPalOnlyTransaction(t.TransactionId, t.Status, t.Amount, t.InvoiceId, t.InitiatedDate))
            .ToList();

        return new ReconciliationReport(from, to, matched, payPalOnly, eShopOnly);
    }
}
