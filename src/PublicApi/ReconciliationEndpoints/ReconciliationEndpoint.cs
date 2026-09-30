using System;
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
/// Operator report: PayPal's own transaction record for [from,to], reconciled against eShop's
/// orders. Covers the full range (31-day chunking + full pagination internally). A range covering
/// payments made moments ago may legitimately come back empty -- PayPal's reporting can lag live
/// activity by up to ~3 hours.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IPaymentService paymentService) =>
            {
                return await HandleAsync(new ReconciliationRequest(from, to), paymentService);
            })
            .Produces<ReconciliationResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("ReconciliationEndpoints");
    }

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IPaymentService paymentService)
    {
        if (request.To < request.From)
        {
            return Results.BadRequest("'to' must not be before 'from'.");
        }

        var response = new ReconciliationResponse(request.CorrelationId());

        var report = await paymentService.ReconcileAsync(request.From, request.To);

        response.From = report.From;
        response.To = report.To;
        response.Matched = new(report.Matched);
        response.InPayPalOnly = new(report.InPayPalOnly);
        response.InEShopOnly = new(report.InEShopOnly);

        return Results.Ok(response);
    }
}
