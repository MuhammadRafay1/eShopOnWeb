using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives Upvest webhook callbacks (Flow 4 settlement). This is the only route Upvest itself calls, so it
/// takes no shopper token. The payload is treated only as a hint: for every id it mentions, the matching
/// investor is reconciled by re-reading the authoritative Upvest API — the callback body is never trusted
/// as the source of truth. Always answers 200 so Upvest does not retry a handled event.
/// </summary>
public class UpvestCallbackEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/callbacks",
            [AllowAnonymous] async (HttpContext http, IInvestingService investing) =>
            {
                string body;
                using (var reader = new StreamReader(http.Request.Body))
                {
                    body = await reader.ReadToEndAsync(http.RequestAborted);
                }

                foreach (var id in ExtractGuids(body))
                {
                    await investing.ReconcileAsync(id, http.RequestAborted);
                }

                return Results.Ok();
            })
            .WithTags(InvestingHttp.Tag);
    }

    private static IEnumerable<Guid> ExtractGuids(string body)
    {
        var seen = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(body)) return seen;

        try
        {
            using var doc = JsonDocument.Parse(body);
            Walk(doc.RootElement, seen);
        }
        catch (JsonException)
        {
            // A non-JSON body carries no ids we can act on; acknowledge and move on.
        }

        return seen;
    }

    private static void Walk(JsonElement element, HashSet<Guid> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    Walk(property.Value, into);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    Walk(item, into);
                break;
            case JsonValueKind.String:
                if (Guid.TryParse(element.GetString(), out var id))
                    into.Add(id);
                break;
        }
    }
}
