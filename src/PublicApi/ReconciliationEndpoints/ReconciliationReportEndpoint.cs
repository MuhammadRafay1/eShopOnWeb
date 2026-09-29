using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

public class ReconciliationRequest : BaseRequest
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
}

public class ReconciliationResponse : BaseResponse
{
    public ReconciliationResponse(Guid correlationId) : base(correlationId) { }
    public ReconciliationResponse() { }

    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public int PayPalTransactionCount { get; set; }
    public int MatchedCount { get; set; }
    public int PayPalOnlyCount { get; set; }
    public int EShopOnlyCount { get; set; }
    public List<ReconciliationMatchDto> Matched { get; set; } = new();
    public List<PayPalOnlyDto> PayPalOnly { get; set; } = new();
    public List<EShopOnlyDto> EShopOnly { get; set; } = new();
}

public class ReconciliationMatchDto
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = "";
    public string? PayPalOrderId { get; set; }
    public string? PayPalTransactionId { get; set; }
    public string? TransactionStatus { get; set; }
    public decimal? PayPalAmount { get; set; }
    public string? CurrencyCode { get; set; }
}

public class PayPalOnlyDto
{
    public string? TransactionId { get; set; }
    public string? InvoiceId { get; set; }
    public string? PayPalReferenceId { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Status { get; set; }
}

public class EShopOnlyDto
{
    public int OrderId { get; set; }
    public string OrderStatus { get; set; } = "";
    public string? PayPalOrderId { get; set; }
    public string? CaptureId { get; set; }
    public decimal? CapturedAmount { get; set; }
}

/// <summary>
/// GET /api/reconciliation?from={iso}&amp;to={iso} — operator action. Lists PayPal's own record of
/// transactions for the range and lines them up against eShop orders (covers the whole range, not
/// just the first page).
/// </summary>
public class ReconciliationReportEndpoint : IEndpoint<IResult, ReconciliationRequest, IReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset? from, DateTimeOffset? to, IReconciliationService service) =>
            {
                if (from is null || to is null)
                {
                    return Results.BadRequest(new { message = "Both 'from' and 'to' ISO-8601 date-times are required." });
                }
                return await HandleAsync(new ReconciliationRequest { From = from.Value, To = to.Value }, service);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("ReconciliationEndpoints");
    }

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IReconciliationService service)
    {
        var report = await service.ReconcileAsync(request.From, request.To);

        var response = new ReconciliationResponse(request.CorrelationId())
        {
            From = report.From,
            To = report.To,
            PayPalTransactionCount = report.PayPalTransactionCount,
            MatchedCount = report.Matched.Count,
            PayPalOnlyCount = report.PayPalOnly.Count,
            EShopOnlyCount = report.EShopOnly.Count
        };

        foreach (var m in report.Matched)
        {
            response.Matched.Add(new ReconciliationMatchDto
            {
                OrderId = m.OrderId,
                OrderStatus = m.OrderStatus,
                PayPalOrderId = m.PayPalOrderId,
                PayPalTransactionId = m.PayPalTransactionId,
                TransactionStatus = m.TransactionStatus,
                PayPalAmount = m.PayPalAmount,
                CurrencyCode = m.CurrencyCode
            });
        }
        foreach (var t in report.PayPalOnly)
        {
            response.PayPalOnly.Add(new PayPalOnlyDto
            {
                TransactionId = t.TransactionId,
                InvoiceId = t.InvoiceId,
                PayPalReferenceId = t.PayPalReferenceId,
                Amount = t.Amount,
                CurrencyCode = t.CurrencyCode,
                Status = t.Status
            });
        }
        foreach (var e in report.EShopOnly)
        {
            response.EShopOnly.Add(new EShopOnlyDto
            {
                OrderId = e.OrderId,
                OrderStatus = e.OrderStatus,
                PayPalOrderId = e.PayPalOrderId,
                CaptureId = e.CaptureId,
                CapturedAmount = e.CapturedAmount
            });
        }

        return Results.Ok(response);
    }
}
