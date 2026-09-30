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
/// Operator report lining up PayPal's own transaction records for [from, to] against eShop's
/// captured orders, covering the whole range (chunked/paged internally, not just page one).
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IPaymentService, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (System.DateTimeOffset from, System.DateTimeOffset to, IPaymentService paymentService, HttpContext httpContext) =>
            {
                return await HandleAsync(new ReconciliationRequest(from, to), paymentService, httpContext);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("ReconciliationEndpoints");
    }

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IPaymentService paymentService, HttpContext httpContext)
    {
        if (request.From > request.To)
        {
            return Results.BadRequest("'from' must not be after 'to'.");
        }

        var report = await paymentService.GetReconciliationReportAsync(request.From, request.To, httpContext.RequestAborted);

        var response = new ReconciliationResponse(request.CorrelationId())
        {
            From = report.From,
            To = report.To,
            Matched = report.Matched.Select(m => new ReconciliationMatchDto
            {
                OrderId = m.OrderId,
                TransactionId = m.TransactionId,
                EShopCapturedAmount = m.EShopCapturedAmount,
                PayPalAmount = m.PayPalAmount,
                PayPalFeeAmount = m.PayPalFeeAmount,
                PayPalStatus = m.PayPalStatus
            }).ToList(),
            PayPalOnly = report.PayPalOnly.Select(t => new ReconciliationPayPalOnlyDto
            {
                TransactionId = t.TransactionId,
                Status = t.Status,
                InvoiceId = t.InvoiceId,
                CustomField = t.CustomField,
                Amount = t.Amount,
                CurrencyCode = t.CurrencyCode,
                InitiationDate = t.InitiationDate
            }).ToList(),
            EShopOnly = report.EShopOnly.Select(e => new ReconciliationEShopOnlyDto
            {
                OrderId = e.OrderId,
                CaptureId = e.CaptureId,
                CapturedAmount = e.CapturedAmount
            }).ToList()
        };
        response.Counts = new ReconciliationCountsDto
        {
            Matched = response.Matched.Count,
            PayPalOnly = response.PayPalOnly.Count,
            EShopOnly = response.EShopOnly.Count
        };

        return Results.Ok(response);
    }
}
