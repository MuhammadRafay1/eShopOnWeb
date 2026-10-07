using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/investing/upvest-webhook — the callback Upvest invokes when a status changes. It is the one
/// route that does not carry a shopper token (Upvest calls it, not a shopper). The payload only signals
/// "something changed"; the handler reconciles from Upvest's authoritative state, so it never has to
/// trust the body's contents. Always acknowledges with 200 so Upvest does not retry-storm.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, HttpContext, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest-webhook",
            [AllowAnonymous] async
            (HttpContext http, IInvestingService investing) => await HandleAsync(http, investing))
            .Produces(StatusCodes.Status200OK)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http, IInvestingService investing)
    {
        try
        {
            await investing.ReconcileAsync(http.RequestAborted);
        }
        catch
        {
            // The timed reconciliation will catch up; acknowledge regardless.
        }

        return Results.Ok();
    }
}
