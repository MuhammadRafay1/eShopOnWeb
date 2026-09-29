using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.PayPal;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IReadRepository<Payment> _paymentRepository;
    private readonly IPayPalClient _payPalClient;

    public ReconciliationService(IReadRepository<Payment> paymentRepository, IPayPalClient payPalClient)
    {
        _paymentRepository = paymentRepository;
        _payPalClient = payPalClient;
    }

    public async Task<ReconciliationReport> ReconcileAsync(DateTimeOffset from, DateTimeOffset to)
    {
        if (to <= from)
            throw new PaymentValidationException("'to' must be later than 'from'.");

        // PayPal's own record for the range (client covers the whole range via windowing + paging).
        var transactions = await _payPalClient.SearchTransactionsAsync(from, to);

        // Local side: all payments (to know every local order id), plus the in-range subset.
        var allPayments = await _paymentRepository.ListAsync();
        var localOrderIds = allPayments.Select(p => p.OrderId).ToHashSet();
        var inRangePayments = allPayments
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .ToList();

        var matched = new List<MatchedTransaction>();
        var onlyInPayPal = new List<PayPalOnlyTransaction>();
        var matchedOrderIds = new HashSet<int>();

        foreach (var txn in transactions)
        {
            if (TryParseOrderId(txn.CustomField, out var orderId) && localOrderIds.Contains(orderId))
            {
                var localPayment = allPayments.First(p => p.OrderId == orderId);
                matched.Add(new MatchedTransaction(orderId, txn.TransactionId, txn.Amount,
                    localPayment.CapturedAmount ?? localPayment.Amount, txn.Status));
                matchedOrderIds.Add(orderId);
            }
            else
            {
                onlyInPayPal.Add(new PayPalOnlyTransaction(txn.TransactionId, txn.CustomField, txn.Amount, txn.Status));
            }
        }

        var onlyInEShop = inRangePayments
            .Where(p => !matchedOrderIds.Contains(p.OrderId))
            .Select(p => new LocalOnlyPayment(p.OrderId, p.CapturedAmount ?? p.Amount, p.Status.ToString()))
            .ToList();

        return new ReconciliationReport(from, to, matched, onlyInEShop, onlyInPayPal);
    }

    private static bool TryParseOrderId(string? customField, out int orderId)
    {
        orderId = 0;
        return !string.IsNullOrWhiteSpace(customField)
               && int.TryParse(customField, NumberStyles.Integer, CultureInfo.InvariantCulture, out orderId);
    }
}
