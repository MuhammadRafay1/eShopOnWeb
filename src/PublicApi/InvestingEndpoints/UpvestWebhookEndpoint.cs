using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/investing/upvest-webhook — the callback Upvest posts status-change events to. This is the
/// only route that does not carry a shopper token; it is addressed by Upvest, not by a shopper. It
/// acknowledges quickly and reconciles the affected investor in the background. Reconciliation on read
/// remains the authoritative path, so this endpoint is purely an accelerator.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, JsonElement, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest-webhook",
            async (JsonElement payload, IInvestingService service) => await HandleAsync(payload, service))
            .AllowAnonymous()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(JsonElement payload, IInvestingService service)
    {
        var userIds = new List<string>();
        CollectUserIds(payload, userIds);
        await service.HandleUpvestEventAsync(userIds);
        return Results.Ok();
    }

    /// <summary>Collects every <c>user_id</c> value anywhere in the event payload.</summary>
    private static void CollectUserIds(JsonElement element, List<string> userIds)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("user_id") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = property.Value.GetString();
                        if (!string.IsNullOrEmpty(value)) userIds.Add(value);
                    }

                    CollectUserIds(property.Value, userIds);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectUserIds(item, userIds);
                }
                break;
        }
    }
}
