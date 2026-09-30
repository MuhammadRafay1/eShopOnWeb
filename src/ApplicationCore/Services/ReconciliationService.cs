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

public class ReconciliationService : IReconciliationService
{
    private readonly IReadRepository<Order> _orderRepository;
    private readonly IPaymentGateway _gateway;

    public ReconciliationService(IReadRepository<Order> orderRepository, IPaymentGateway gateway)
    {
        _orderRepository = orderRepository;
        _gateway = gateway;
    }

    public async Task<ReconciliationReport> GetReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var transactions = await _gateway.SearchTransactionsAsync(from, to, ct);

        var orders = await _orderRepository.ListAsync(new OrdersWithPaymentSpecification(), ct);
        var eshopPayments = orders
            .Where(o => o.Payment is not null)
            .Select(o => (Order: o, Payment: o.Payment!))
            .ToList();

        var matched = new List<ReconciliationMatch>();
        var matchedTransactionIds = new HashSet<string>();
        var matchedInvoiceReferences = new HashSet<string>();

        foreach (var (order, payment) in eshopPayments)
        {
            var match = transactions.FirstOrDefault(t => t.InvoiceId == payment.InvoiceReference);
            if (match is null)
            {
                continue;
            }

            matched.Add(new ReconciliationMatch(
                order.Id,
                payment.InvoiceReference,
                match.TransactionId,
                payment.CapturedAmount ?? payment.AuthorizedAmount,
                match.Amount,
                match.Currency,
                match.Status));
            matchedTransactionIds.Add(match.TransactionId);
            matchedInvoiceReferences.Add(payment.InvoiceReference);
        }

        var unmatchedInPayPal = transactions.Where(t => !matchedTransactionIds.Contains(t.TransactionId)).ToList();
        var unmatchedInEshop = eshopPayments
            .Where(x => !matchedInvoiceReferences.Contains(x.Payment.InvoiceReference))
            .Select(x => new UnmatchedEshopPayment(x.Order.Id, x.Payment.InvoiceReference, x.Payment.CapturedAmount ?? x.Payment.AuthorizedAmount, x.Payment.CurrencyCode, x.Order.PaymentStatus.ToString()))
            .ToList();

        return new ReconciliationReport(from, to, transactions.Count, matched, unmatchedInPayPal, unmatchedInEshop);
    }
}
