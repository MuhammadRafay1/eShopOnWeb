using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

/// <summary>
/// Operator report: lines PayPal's own transaction record up against eShop's orders for [from, to], so a
/// payment PayPal knows about and eShop doesn't - or the reverse - is visible. An empty result over a
/// just-created range is expected PayPal sandbox reporting lag, not a failure.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, DateTimeOffset, DateTimeOffset, IReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IReconciliationService reconciliationService) =>
            {
                return await HandleAsync(from, to, reconciliationService);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("ReconciliationEndpoints");
    }

    public async Task<IResult> HandleAsync(DateTimeOffset from, DateTimeOffset to, IReconciliationService reconciliationService)
    {
        if (to < from)
        {
            return Results.BadRequest("'to' must not be earlier than 'from'.");
        }

        var report = await reconciliationService.BuildReportAsync(from, to, default);

        var response = new ReconciliationResponse(Guid.NewGuid())
        {
            From = report.From,
            To = report.To,
            Complete = report.Complete,
            Matched = report.Matched.Select(m => new ReconciliationMatchDto
            {
                Order = new ReconciliationOrderSideDto
                {
                    OrderId = m.Order.OrderId,
                    PayPalOrderId = m.Order.PayPalOrderId,
                    CaptureId = m.Order.CaptureId,
                    CapturedAmount = m.Order.CapturedAmount,
                    Status = m.Order.Status
                },
                PayPal = new ReconciliationPayPalSideDto
                {
                    TransactionId = m.PayPal.TransactionId,
                    Amount = m.PayPal.Amount,
                    Status = m.PayPal.Status,
                    InvoiceId = m.PayPal.InvoiceId,
                    CustomField = m.PayPal.CustomField
                }
            }).ToArray(),
            PayPalOnly = report.PayPalOnly.Select(p => new ReconciliationPayPalSideDto
            {
                TransactionId = p.TransactionId,
                Amount = p.Amount,
                Status = p.Status,
                InvoiceId = p.InvoiceId,
                CustomField = p.CustomField
            }).ToArray(),
            EShopOnly = report.EShopOnly.Select(o => new ReconciliationOrderSideDto
            {
                OrderId = o.OrderId,
                PayPalOrderId = o.PayPalOrderId,
                CaptureId = o.CaptureId,
                CapturedAmount = o.CapturedAmount,
                Status = o.Status
            }).ToArray()
        };
        return Results.Ok(response);
    }
}
