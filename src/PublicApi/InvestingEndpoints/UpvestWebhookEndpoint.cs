using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/investing/upvest/webhook — the single route Upvest itself calls. It carries no shopper
/// token (it is Upvest-to-server). Receiving any event prompts reconciliation, which settles pending
/// investments and advances enrolments against their current Upvest state. Returns 200 so Upvest does
/// not retry; reconciliation is idempotent and also runs on a timer, so a missed webhook is harmless.
///
/// A production deployment would additionally verify the webhook's signature against Upvest's JWKS
/// (GET /auth/verify_keys) before acting.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, HttpContext, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest/webhook",
            async (HttpContext http, IInvestingService service) => await HandleAsync(http, service))
            .AllowAnonymous()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http, IInvestingService service)
    {
        // Drain the body (payload shape is not relied upon; reconciliation reads authoritative state).
        using (var reader = new StreamReader(http.Request.Body))
        {
            await reader.ReadToEndAsync(http.RequestAborted);
        }

        try
        {
            // Settle only — a webhook must never drive new orders (that would feed back on itself).
            await service.SettlePendingAsync(http.RequestAborted);
        }
        catch
        {
            // Never fail a webhook back to Upvest; the timer will reconcile regardless.
        }

        return Results.Ok();
    }
}
