using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Operator report: PayPal's own record of transactions for a date range, lined up against eShop
/// orders, so a payment either side knows about and the other doesn't is visible.
/// </summary>
public class ReconciliationEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IRepository<Order> orderRepository, IPayPalPaymentGateway gateway) =>
            {
                return await HandleAsync(from, to, orderRepository, gateway);
            })
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(DateTimeOffset from, DateTimeOffset to, IRepository<Order> orderRepository, IPayPalPaymentGateway gateway)
    {
        if (to < from)
        {
            return Results.BadRequest("'to' must not be before 'from'.");
        }

        ReconciliationResult paypal;
        try
        {
            paypal = await gateway.SearchTransactionsAsync(from, to, default);
        }
        catch (PaymentGatewayException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }

        var capturedOrders = await orderRepository.ListAsync(new OrdersWithCapturedPaymentSpecification());
        var ordersInRange = capturedOrders
            .Where(o => o.Payment!.CapturedAt is DateTimeOffset capturedAt && capturedAt >= from && capturedAt <= to)
            .ToList();

        // PayPal's own transaction id is not the capture id, so orders are correlated by the invoice id
        // stamped on the purchase unit at authorization time (the order's own AuthorizeIdempotencyKey)
        // and echoed back on the transaction-search record.
        var paypalInvoiceIds = paypal.Transactions.Where(t => t.InvoiceId is not null)
            .Select(t => t.InvoiceId!).ToHashSet();
        var eshopInvoiceIds = ordersInRange.Select(o => o.Payment!.AuthorizeIdempotencyKey).ToHashSet();

        var matched = ordersInRange.Where(o => paypalInvoiceIds.Contains(o.Payment!.AuthorizeIdempotencyKey))
            .Select(o => new { orderId = o.Id, captureId = o.Payment!.CaptureId, amount = o.Payment.CapturedAmount });

        var eshopOnly = ordersInRange.Where(o => !paypalInvoiceIds.Contains(o.Payment!.AuthorizeIdempotencyKey))
            .Select(o => new { orderId = o.Id, captureId = o.Payment!.CaptureId, amount = o.Payment.CapturedAmount });

        var paypalOnly = paypal.Transactions.Where(t => t.InvoiceId is null || !eshopInvoiceIds.Contains(t.InvoiceId));

        return Results.Ok(new
        {
            from,
            to,
            truncated = paypal.Truncated,
            pagesRead = paypal.PagesRead,
            totalPages = paypal.TotalPages,
            matched,
            eshopOnly,
            paypalOnly
        });
    }
}
