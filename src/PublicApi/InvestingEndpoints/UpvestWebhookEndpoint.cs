using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives event notifications from Upvest. This is the only route not driven by a shopper's
/// token, so it is anonymous. It carries no trust of its own: a notification simply prompts the
/// shop to re-read the authoritative state from Upvest and reconcile. The background reconciler
/// does the same on a timer, so settlement is correct even if a notification never arrives.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest-events",
            [AllowAnonymous]
            async (IInvestingService investing, CancellationToken cancellationToken) =>
            {
                await investing.ReconcileAllAsync(cancellationToken);
                return Results.Ok();
            })
            .WithTags("InvestingEndpoints");

        // Some providers probe the endpoint with a GET before enabling it.
        app.MapGet("api/investing/upvest-events",
            [AllowAnonymous]
            () => Results.Ok())
            .WithTags("InvestingEndpoints");
    }
}
