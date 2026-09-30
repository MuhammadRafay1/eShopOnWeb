using System;
using System.Threading;
using System.Threading.Tasks;
using BlazorShared.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.ReconciliationEndpoints;

/// <summary>
/// Operator action: lists PayPal's own transactions for a date range lined up against eShop orders.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string? from, string? to, IReconciliationService reconciliationService) =>
            {
                if (!DateTimeOffset.TryParse(from, out var parsedFrom) || !DateTimeOffset.TryParse(to, out var parsedTo))
                {
                    return Results.BadRequest("from and to must be valid ISO-8601 date-times.");
                }

                return await HandleAsync(new ReconciliationRequest(parsedFrom, parsedTo), reconciliationService);
            })
            .Produces<ReconciliationResponse>()
            .WithTags("ReconciliationEndpoints");
    }

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IReconciliationService reconciliationService)
    {
        var report = await reconciliationService.GetReportAsync(request.From, request.To, CancellationToken.None);

        var response = new ReconciliationResponse(request.CorrelationId())
        {
            From = report.From,
            To = report.To,
            PayPalTransactionCount = report.PayPalTransactionCount,
            Matched = new(report.Matched),
            UnmatchedInPayPal = new(report.UnmatchedInPayPal),
            UnmatchedInEshop = new(report.UnmatchedInEshop)
        };

        return Results.Ok(response);
    }
}
