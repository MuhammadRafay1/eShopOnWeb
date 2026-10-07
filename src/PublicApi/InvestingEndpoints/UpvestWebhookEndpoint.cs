using System;
using System.Collections.Generic;
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
/// Receives Upvest order-event callbacks. This is the only route that does not take a shopper token (Upvest
/// calls it). The payload is treated purely as a hint: for every order id it mentions we re-read the order's
/// authoritative status from Upvest rather than trusting the body, so an unverified or forged callback cannot
/// corrupt state — the worst it can do is trigger a harmless re-read.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, HttpContext, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest/webhook",
            async (HttpContext context, IInvestingService investing) => await HandleAsync(context, investing))
            .AllowAnonymous()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext context, IInvestingService investing)
    {
        string body;
        using (var reader = new StreamReader(context.Request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        foreach (var orderId in ExtractOrderIds(body))
        {
            await investing.ReconcileOrderAsync(orderId);
        }

        // Always acknowledge: reconciliation also happens on read, so a payload we could not parse is not fatal.
        return Results.Ok();
    }

    /// <summary>
    /// Collect every GUID-shaped value under a property whose name looks like an id, regardless of the exact
    /// payload shape. Unknown ids are ignored downstream, so over-collecting is safe.
    /// </summary>
    private static IEnumerable<string> ExtractOrderIds(string body)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(body))
        {
            return ids;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            Walk(document.RootElement, parentName: null, ids);
        }
        catch (JsonException)
        {
            // Not JSON we can read — read-time reconciliation will catch up.
        }

        return ids;
    }

    private static void Walk(JsonElement element, string? parentName, HashSet<string> ids)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, property.Name, ids);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, parentName, ids);
                }
                break;
            case JsonValueKind.String:
                if (parentName is not null &&
                    parentName.Contains("id", StringComparison.OrdinalIgnoreCase) &&
                    Guid.TryParse(element.GetString(), out _))
                {
                    ids.Add(element.GetString()!);
                }
                break;
        }
    }
}
