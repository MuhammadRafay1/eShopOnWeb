using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives webhook deliveries from Upvest (Flow 4). This is the only route Upvest calls, and the only one
/// that does not take a shopper token. The delivery's HTTP Message Signature is verified against Upvest's
/// JWKS. State is not changed from the payload: the background reconciler re-reads authoritative state from
/// Upvest, so a verified delivery simply confirms (and, in production, would accelerate) that reconcile.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, HttpContext>
{
    private readonly UpvestWebhookVerifier _verifier;
    private readonly IAppLogger<UpvestWebhookEndpoint> _logger;
    private readonly UpvestOptions _options;

    public UpvestWebhookEndpoint(UpvestWebhookVerifier verifier, IAppLogger<UpvestWebhookEndpoint> logger, UpvestOptions options)
    {
        _verifier = verifier;
        _logger = logger;
        _options = options;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost(_options.WebhookPath, async (HttpContext ctx) => await HandleAsync(ctx))
            .AllowAnonymous()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext ctx)
    {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        var body = ms.ToArray();

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in ctx.Request.Headers)
            headers[h.Key] = h.Value.ToString();

        var verified = await _verifier.VerifyAsync("POST", ctx.Request.Path.Value ?? _options.WebhookPath, headers, body, ctx.RequestAborted);
        if (verified)
            _logger.LogInformation("Received a verified Upvest webhook delivery.");
        else
            _logger.LogWarning("Received an Upvest webhook delivery whose signature could not be verified; ignoring its payload (state is reconciled from Upvest directly).");

        // Always 200 so the sender does not retry; state is driven by the reconciler, not the payload.
        return Results.Ok();
    }
}
