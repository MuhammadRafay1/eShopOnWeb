using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives Upvest webhook callbacks. This is the one route Upvest itself calls, so it is not
/// JWT-authenticated and carries no shopper token; the events carry Upvest's own ids. Order/execution
/// events trigger reconciliation of the matching investment, so an investment's status reflects what
/// actually happened at Upvest even between shopper reads. Always acknowledges with 200.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, HttpRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        // No [Authorize]: Upvest calls this, not the shopper.
        app.MapPost("api/investing/upvest-webhook",
            async (HttpRequest request, IInvestingService investing) => await HandleAsync(request, investing))
            .AllowAnonymous()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpRequest request, IInvestingService investing)
    {
        string body;
        using (var reader = new StreamReader(request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Array)
            {
                foreach (var evt in payload.EnumerateArray())
                {
                    var orderId = ExtractOrderId(evt);
                    if (!string.IsNullOrEmpty(orderId))
                    {
                        await investing.ReconcileByUpvestOrderAsync(orderId!);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // A body we cannot parse is still acknowledged; reconciliation also happens on read.
        }

        return Results.Ok();
    }

    /// <summary>Pulls an Upvest order id out of an event's <c>object</c>, if the event carries one.</summary>
    private static string? ExtractOrderId(JsonElement evt)
    {
        if (!evt.TryGetProperty("object", out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in new[] { "order_id", "id" })
        {
            if (obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }
}
