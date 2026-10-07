using System;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/investing/upvest-webhook — the callback Upvest posts status-change events to.
/// This is the only route that is not authenticated with the shopper's token (Upvest has no
/// shopper token). It acknowledges quickly and reconciles enrolments and investments against
/// Upvest's current state. Reconciliation is also run on a timer, so a missed webhook is not
/// a problem; this just makes updates prompt.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest-webhook",
            [AllowAnonymous]
            async (IInvestingService investingService, ILoggerFactory loggerFactory, CancellationToken ct) =>
            {
                try
                {
                    await investingService.ReconcileAsync(ct);
                }
                catch (Exception ex)
                {
                    // Always acknowledge; a failed reconcile is retried by the timer.
                    loggerFactory.CreateLogger<UpvestWebhookEndpoint>()
                        .LogWarning("Webhook-triggered reconcile failed: {Message}", ex.Message);
                }

                return Results.Ok();
            })
            .WithTags("InvestingEndpoints");
    }
}
