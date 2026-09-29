using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.PaymentAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Payments;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

/// <summary>
/// Operator report lining up PayPal's own record of transactions for a date range against eShop
/// orders, so a payment PayPal knows about that eShop doesn't - or the reverse - is visible.
/// Covers the whole range (the gateway chunks/paginates internally). Admin-only.
/// </summary>
public class ReconciliationReportEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                string? from,
                string? to,
                IReadRepository<Order> orderRepository,
                IReadRepository<Payment> paymentRepository,
                IPayPalReconciliationGateway gateway,
                CancellationToken ct) =>
            {
                return await HandleAsync(from, to, orderRepository, paymentRepository, gateway, ct);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("ReconciliationEndpoints");
    }

    private static async Task<IResult> HandleAsync(string? from, string? to,
        IReadRepository<Order> orderRepository, IReadRepository<Payment> paymentRepository,
        IPayPalReconciliationGateway gateway, CancellationToken ct)
    {
        if (!TryParse(from, out var fromDate) || !TryParse(to, out var toDate))
        {
            return Results.BadRequest("'from' and 'to' must be ISO-8601 date-times.");
        }
        if (fromDate > toDate)
        {
            return Results.BadRequest("'from' must not be after 'to'.");
        }

        var transactions = await gateway.SearchTransactionsAsync(fromDate, toDate, ct);

        // Local payments in range, joined to their orders for the activity date and status.
        var orders = (await orderRepository.ListAsync(ct)).ToDictionary(o => o.Id);
        var payments = await paymentRepository.ListAsync(ct);

        // Only payments that actually reached PayPal (have an invoice id) are reconcilable.
        var localInRange = payments
            .Where(p => p.InvoiceId is not null
                && orders.TryGetValue(p.OrderId, out var o)
                && o.OrderDate >= fromDate && o.OrderDate <= toDate)
            .ToList();
        var localByInvoice = localInRange.ToDictionary(p => p.InvoiceId!);

        var matched = new List<MatchedItem>();
        var payPalOnly = new List<PayPalTransactionItem>();
        var seenLocalInvoices = new HashSet<string>();

        foreach (var txn in transactions)
        {
            var invoice = txn.InvoiceId;
            if (invoice is not null && localByInvoice.TryGetValue(invoice, out var payment))
            {
                seenLocalInvoices.Add(invoice);
                orders.TryGetValue(payment.OrderId, out var order);
                matched.Add(new MatchedItem
                {
                    OrderId = payment.OrderId,
                    TransactionId = txn.TransactionId,
                    InvoiceId = invoice,
                    PayPalAmount = txn.Amount,
                    PayPalStatus = txn.Status,
                    InitiationDate = txn.InitiationDate,
                    LocalAuthorizedAmount = payment.AuthorizedAmount,
                    LocalCapturedAmount = payment.CapturedGrossAmount,
                    OrderStatus = order?.Status.ToString()
                });
            }
            else
            {
                payPalOnly.Add(new PayPalTransactionItem
                {
                    TransactionId = txn.TransactionId,
                    InvoiceId = invoice,
                    Amount = txn.Amount,
                    Status = txn.Status,
                    InitiationDate = txn.InitiationDate
                });
            }
        }

        var eShopOnly = localInRange
            .Where(p => !seenLocalInvoices.Contains(p.InvoiceId!))
            .Select(p =>
            {
                orders.TryGetValue(p.OrderId, out var order);
                return new EShopOnlyItem
                {
                    OrderId = p.OrderId,
                    OrderStatus = order?.Status.ToString(),
                    AuthorizationStatus = p.AuthorizationStatus.ToString(),
                    CaptureStatus = p.CaptureStatus?.ToString(),
                    AuthorizedAmount = p.AuthorizedAmount,
                    CapturedAmount = p.CapturedGrossAmount,
                    OrderDate = order?.OrderDate
                };
            })
            .ToList();

        var response = new ReconciliationResponse
        {
            From = fromDate,
            To = toDate,
            PayPalTransactionCount = transactions.Count,
            MatchedCount = matched.Count,
            PayPalOnlyCount = payPalOnly.Count,
            EShopOnlyCount = eShopOnly.Count,
            Matched = matched,
            PayPalOnly = payPalOnly,
            EShopOnly = eShopOnly
        };
        return Results.Ok(response);
    }

    private static bool TryParse(string? value, out DateTimeOffset result)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
    }
}

public class ReconciliationResponse
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public int PayPalTransactionCount { get; set; }
    public int MatchedCount { get; set; }
    public int PayPalOnlyCount { get; set; }
    public int EShopOnlyCount { get; set; }
    public IReadOnlyList<MatchedItem> Matched { get; set; } = Array.Empty<MatchedItem>();
    public IReadOnlyList<PayPalTransactionItem> PayPalOnly { get; set; } = Array.Empty<PayPalTransactionItem>();
    public IReadOnlyList<EShopOnlyItem> EShopOnly { get; set; } = Array.Empty<EShopOnlyItem>();
}

public class MatchedItem
{
    public int OrderId { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string? InvoiceId { get; set; }
    public decimal PayPalAmount { get; set; }
    public string? PayPalStatus { get; set; }
    public DateTimeOffset InitiationDate { get; set; }
    public decimal LocalAuthorizedAmount { get; set; }
    public decimal? LocalCapturedAmount { get; set; }
    public string? OrderStatus { get; set; }
}

public class PayPalTransactionItem
{
    public string TransactionId { get; set; } = string.Empty;
    public string? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset InitiationDate { get; set; }
}

public class EShopOnlyItem
{
    public int OrderId { get; set; }
    public string? OrderStatus { get; set; }
    public string AuthorizationStatus { get; set; } = string.Empty;
    public string? CaptureStatus { get; set; }
    public decimal AuthorizedAmount { get; set; }
    public decimal? CapturedAmount { get; set; }
    public DateTimeOffset? OrderDate { get; set; }
}
