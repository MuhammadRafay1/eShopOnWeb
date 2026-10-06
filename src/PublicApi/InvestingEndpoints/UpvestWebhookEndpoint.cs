using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Flow 4 — the route Upvest calls (no shopper token). An inbound notification only nudges reconciliation;
/// authoritative status always comes from a signed provider read, so an unverified call cannot spoof state.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest-webhook",
            async (HttpContext context, IInvestmentReconciler reconciler, ILoggerFactory loggerFactory,
                   CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("UpvestWebhook");
                logger.LogInformation("Received Upvest webhook notification; triggering reconciliation.");

                // Drain the body (not trusted for state) then reconcile against the provider.
                context.Request.Body.Position = context.Request.Body.CanSeek ? 0 : context.Request.Body.Position;

                try
                {
                    await reconciler.ReconcileAllAsync(ct);
                }
                catch (System.Exception ex)
                {
                    logger.LogWarning(ex, "Reconciliation triggered by webhook failed; the timer will retry.");
                }

                return Results.Ok();
            })
            .WithTags("InvestingEndpoints");
    }

    public Task<IResult> HandleAsync() => Task.FromResult<IResult>(Results.Empty);
}
