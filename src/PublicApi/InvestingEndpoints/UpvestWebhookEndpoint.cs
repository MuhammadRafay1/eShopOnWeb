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
/// Receives Upvest webhook callbacks (POST api/investing/upvest-webhook). This is the one
/// route Upvest itself calls, so it is not shopper-authenticated. Any order referenced by
/// the callback is re-reconciled against Upvest so investment statuses stay current. The
/// read-time reconciliation on the balance/investments endpoints is the primary mechanism;
/// this callback is a best-effort push path.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, HttpContext, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest-webhook",
            async (HttpContext context, IInvestingService investingService) => await HandleAsync(context, investingService))
            .WithTags("InvestingEndpoints")
            .AllowAnonymous();
    }

    public async Task<IResult> HandleAsync(HttpContext context, IInvestingService investingService)
    {
        string body;
        using (var reader = new StreamReader(context.Request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        // The webhook body is not strongly modelled; reconcile every order-shaped id it carries.
        foreach (var candidate in ExtractGuids(body))
        {
            await investingService.ReconcileByProviderOrderAsync(candidate);
        }

        // Always acknowledge so Upvest does not retry indefinitely.
        return Results.Ok();
    }

    private static IEnumerable<string> ExtractGuids(string json)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return seen;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            Walk(doc.RootElement, seen);
        }
        catch (JsonException)
        {
            // Not JSON we can read; nothing to reconcile from it.
        }

        return seen;
    }

    private static void Walk(JsonElement element, HashSet<string> acc)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    Walk(prop.Value, acc);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, acc);
                }
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out _))
                {
                    acc.Add(value);
                }
                break;
        }
    }
}
