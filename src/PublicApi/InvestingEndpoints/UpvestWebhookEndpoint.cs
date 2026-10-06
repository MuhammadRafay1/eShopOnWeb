using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Envelope Upvest posts to the webhook callback. Events arrive batched under <c>payload</c>;
/// a single-event body (with <c>type</c> at the root) is also tolerated.
/// </summary>
public class UpvestWebhookEnvelope
{
    public List<UpvestWebhookEvent>? Payload { get; set; }

    public string? Type { get; set; }
    public JsonElement Object { get; set; }
}

public class UpvestWebhookEvent
{
    public string? Type { get; set; }
    public JsonElement Object { get; set; }
}

/// <summary>
/// Receives Upvest webhook deliveries. This is the only route Upvest itself calls, so it does not
/// require the shopper's token. Processing is idempotent — duplicate deliveries are safe.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, UpvestWebhookEnvelope, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest-webhook",
            [AllowAnonymous] async (UpvestWebhookEnvelope envelope, IInvestingService investing) =>
            {
                return await HandleAsync(envelope, investing);
            })
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(UpvestWebhookEnvelope envelope, IInvestingService investing)
    {
        var events = new List<UpvestWebhookEvent>();
        if (envelope.Payload is { Count: > 0 })
        {
            events.AddRange(envelope.Payload);
        }
        else if (!string.IsNullOrEmpty(envelope.Type))
        {
            events.Add(new UpvestWebhookEvent { Type = envelope.Type, Object = envelope.Object });
        }

        foreach (var ev in events)
        {
            if (string.IsNullOrEmpty(ev.Type)) continue;
            var resourceId = ExtractResourceId(ev);
            await investing.HandleWebhookEventAsync(ev.Type!, resourceId, default);
        }

        // Always acknowledge so Upvest does not keep retrying a delivery we have accepted.
        return Results.Ok();
    }

    private static string? ExtractResourceId(UpvestWebhookEvent ev)
    {
        if (ev.Object.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var domain = ev.Type!.Split('.')[0];
        if (domain == "EXECUTION" && ev.Object.TryGetProperty("order_id", out var orderId))
        {
            return orderId.GetString();
        }
        if (ev.Object.TryGetProperty("id", out var id))
        {
            return id.GetString();
        }
        return null;
    }
}
