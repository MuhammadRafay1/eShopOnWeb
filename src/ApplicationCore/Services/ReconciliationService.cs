using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Models.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>Lines PayPal's own transaction history up against eShop's payment records for a date range.</summary>
public class ReconciliationService : IReconciliationService
{
    private readonly IPaymentGateway _gateway;
    private readonly IReadRepository<Payment> _paymentRepository;

    public ReconciliationService(IPaymentGateway gateway, IReadRepository<Payment> paymentRepository)
    {
        _gateway = gateway;
        _paymentRepository = paymentRepository;
    }

    public async Task<ReconciliationReport> BuildAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        if (from > to)
        {
            throw new RequestValidationException("'from' must not be after 'to'.");
        }

        // Covers the whole range — the gateway itself pages through every result internally.
        var transactions = await _gateway.SearchTransactionsAsync(from, to, ct);
        var payments = await _paymentRepository.ListAsync(new PaymentsCreatedBetweenSpec(from, to), ct);

        var matched = new List<ReconciliationMatch>();
        var paypalOnly = new List<GatewayTransaction>();
        var matchedPaymentIds = new HashSet<int>();

        foreach (var txn in transactions)
        {
            // Primary correlation key: invoice_id, which round-trips cleanly to CorrelationReference.
            var payment = payments.FirstOrDefault(p => p.CorrelationReference == txn.InvoiceId);
            if (payment is not null)
            {
                matched.Add(new ReconciliationMatch
                {
                    OrderId = payment.OrderId,
                    CorrelationReference = payment.CorrelationReference,
                    EShopAmount = payment.Amount,
                    EShopStatus = payment.Status.ToString(),
                    PayPalTransactionId = txn.TransactionId,
                    PayPalAmount = txn.Amount,
                    PayPalStatus = txn.Status
                });
                matchedPaymentIds.Add(payment.Id);
            }
            else
            {
                paypalOnly.Add(txn);
            }
        }

        var eShopOnly = payments
            .Where(p => !matchedPaymentIds.Contains(p.Id))
            .Select(p => new ReconciliationEShopEntry
            {
                OrderId = p.OrderId,
                CorrelationReference = p.CorrelationReference,
                Amount = p.Amount,
                Status = p.Status.ToString()
            })
            .ToList();

        return new ReconciliationReport
        {
            From = from,
            To = to,
            Matched = matched,
            PayPalOnly = paypalOnly,
            EShopOnly = eShopOnly,
            Note = "PayPal's own transaction reporting lags live activity; an empty PayPal-side result for a range covering just-created payments is expected, not a failure."
        };
    }
}
