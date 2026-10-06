using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives Upvest webhook notifications. This is the only route Upvest itself calls, so it takes no shopper
/// token. The notification is treated purely as a nudge to re-read authoritative status from Upvest
/// (enrolment and investment settlement); it carries no authority of its own, so the handler re-syncs from
/// the provider rather than trusting the payload.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, UpvestWebhookEndpoint.Dependencies>
{
    public record struct Dependencies(IInvestingService Investing, CancellationToken CancellationToken);

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest/webhook",
            [AllowAnonymous] async (
                IInvestingService investing,
                CancellationToken cancellationToken) =>
            {
                return await HandleAsync(new Dependencies(investing, cancellationToken));
            })
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(Dependencies deps)
    {
        await deps.Investing.ReconcileAllAsync(deps.CancellationToken);
        return Results.Ok();
    }
}
