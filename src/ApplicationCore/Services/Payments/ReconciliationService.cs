using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services.Payments;

public class ReconciliationService : IReconciliationService
{
    // Backstop so a misbehaving provider (next-page cursor never advancing) cannot spin forever;
    // a report capped here is marked Truncated rather than silently incomplete.
    private const int MaxPages = 50;
    private const int PageSize = 500;

    private readonly IPayPalPaymentGateway _gateway;
    private readonly IRepository<OrderPayment> _orderPaymentRepository;

    public ReconciliationService(IPayPalPaymentGateway gateway, IRepository<OrderPayment> orderPaymentRepository)
    {
        _gateway = gateway;
        _orderPaymentRepository = orderPaymentRepository;
    }

    public async Task<ReconciliationReport> GetReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var transactions = new List<ReconciliationTransaction>();
        var page = 1;
        var pagesFetched = 0;
        var totalPages = 1;

        while (page <= totalPages && pagesFetched < MaxPages)
        {
            var result = await _gateway.SearchTransactionsAsync(from, to, page, PageSize, cancellationToken);
            transactions.AddRange(result.Transactions);
            totalPages = Math.Max(result.TotalPages, 1);
            pagesFetched++;
            page++;
        }

        var truncated = pagesFetched < totalPages;

        var ourPayments = await _orderPaymentRepository.ListAsync(new OrderPaymentsUpdatedBetweenSpec(from, to), cancellationToken);
        var byOrderId = ourPayments.ToDictionary(p => p.OrderId);
        var matchedOrderIds = new HashSet<int>();

        var entries = new List<ReconciliationEntry>();
        foreach (var txn in transactions)
        {
            var orderId = TryParseOrderId(txn.CustomField) ?? TryParseOrderId(txn.InvoiceId);
            if (orderId.HasValue && byOrderId.TryGetValue(orderId.Value, out var payment))
            {
                matchedOrderIds.Add(orderId.Value);
                entries.Add(new ReconciliationEntry(txn.TransactionId, txn.Status, txn.Amount, txn.CurrencyCode,
                    orderId, payment.Status.ToString(), "Matched"));
            }
            else
            {
                entries.Add(new ReconciliationEntry(txn.TransactionId, txn.Status, txn.Amount, txn.CurrencyCode,
                    orderId, null, "PayPalOnly"));
            }
        }

        foreach (var payment in ourPayments.Where(p => !matchedOrderIds.Contains(p.OrderId)))
        {
            entries.Add(new ReconciliationEntry(null, null, null, payment.CurrencyCode, payment.OrderId,
                payment.Status.ToString(), "EshopOnly"));
        }

        return new ReconciliationReport(entries, pagesFetched, totalPages, truncated);
    }

    private static int? TryParseOrderId(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var text = value.StartsWith("ord-", StringComparison.OrdinalIgnoreCase) ? value[4..] : value;
        return int.TryParse(text, out var id) ? id : null;
    }
}
