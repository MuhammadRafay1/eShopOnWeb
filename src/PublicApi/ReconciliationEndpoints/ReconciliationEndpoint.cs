using System;
using System.Collections.Generic;
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

public record ReconciliationRequest(DateTimeOffset From, DateTimeOffset To);

public class ReconciliationResponse
{
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public List<MatchedDto> Matched { get; set; } = new();
    public List<OnlyInEShopDto> OnlyInEShop { get; set; } = new();
    public List<OnlyInPayPalDto> OnlyInPayPal { get; set; } = new();
}

public class MatchedDto
{
    public int OrderId { get; set; }
    public string PayPalTransactionId { get; set; } = "";
    public decimal PayPalAmount { get; set; }
    public decimal LocalAmount { get; set; }
    public string PayPalStatus { get; set; } = "";
}

public class OnlyInEShopDto
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = "";
}

public class OnlyInPayPalDto
{
    public string PayPalTransactionId { get; set; } = "";
    public string? CustomField { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
}

/// <summary>
/// Operator report: lists PayPal's own record of transactions for a date range and lines them up
/// against eShop orders. Covers the whole range (windowing + paging handled beneath). An empty
/// result for a freshly-created range is an expected sandbox reporting lag, not a failure.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS,
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IReconciliationService service) =>
            {
                if (to <= from)
                    return Results.BadRequest(new { message = "'to' must be later than 'from'." });
                return await HandleAsync(new ReconciliationRequest(from, to), service);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("ReconciliationEndpoints");
    }

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IReconciliationService service)
    {
        var report = await service.ReconcileAsync(request.From, request.To);

        var response = new ReconciliationResponse
        {
            From = report.From,
            To = report.To,
            Matched = report.Matched.Select(m => new MatchedDto
            {
                OrderId = m.OrderId,
                PayPalTransactionId = m.PayPalTransactionId,
                PayPalAmount = m.PayPalAmount,
                LocalAmount = m.LocalAmount,
                PayPalStatus = m.PayPalStatus,
            }).ToList(),
            OnlyInEShop = report.OnlyInEShop.Select(o => new OnlyInEShopDto
            {
                OrderId = o.OrderId,
                Amount = o.Amount,
                PaymentStatus = o.PaymentStatus,
            }).ToList(),
            OnlyInPayPal = report.OnlyInPayPal.Select(o => new OnlyInPayPalDto
            {
                PayPalTransactionId = o.PayPalTransactionId,
                CustomField = o.CustomField,
                Amount = o.Amount,
                Status = o.Status,
            }).ToList(),
        };
        return Results.Ok(response);
    }
}
