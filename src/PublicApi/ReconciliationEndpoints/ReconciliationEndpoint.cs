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
/// Operator action: lines PayPal's own record of transactions up against eShop's orders for a date
/// range, so a payment PayPal knows about and eShop doesn't (or the reverse) is visible.
/// </summary>
public class ReconciliationEndpoint : IEndpoint<IResult, ReconciliationRequest, IReconciliationService>
{
    public void AddRoute(IEndpointRouteBuilder app) =>
        app.MapGet("api/reconciliation",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (DateTimeOffset from, DateTimeOffset to, IReconciliationService reconciliationService) =>
            {
                if (to <= from)
                {
                    return Results.ValidationProblem(new System.Collections.Generic.Dictionary<string, string[]>
                    {
                        ["to"] = new[] { "'to' must be after 'from'." }
                    });
                }
                return await HandleAsync(new ReconciliationRequest(from, to), reconciliationService);
            })
            .Produces<ReconciliationResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .WithTags("ReconciliationEndpoints");

    public async Task<IResult> HandleAsync(ReconciliationRequest request, IReconciliationService reconciliationService)
    {
        var response = new ReconciliationResponse(request.CorrelationId());

        var report = await reconciliationService.BuildReportAsync(request.From, request.To);
        response.From = report.From;
        response.To = report.To;
        response.Entries = ReconciliationDtoMapper.FromDomain(report.Entries);

        return Results.Ok(response);
    }
}
