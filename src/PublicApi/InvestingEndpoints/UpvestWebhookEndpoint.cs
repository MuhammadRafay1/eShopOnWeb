using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.Infrastructure.Investing;
using Microsoft.eShopWeb.Infrastructure.Investing.Webhooks;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives event notifications from Upvest (Flow 4). This is the one route Upvest itself calls,
/// so it is the only endpoint that is not authenticated with a shopper's token. Each event
/// triggers a re-read of the affected resource from Upvest, so the stored state always reflects
/// what actually happened there.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost(UpvestWebhooks.Path,
            [AllowAnonymous] async (HttpRequest httpRequest, IUpvestWebhookService webhookService) =>
                await HandleAsync(httpRequest, webhookService))
            .WithTags("InvestingEndpoints");
    }

    private static async Task<IResult> HandleAsync(HttpRequest httpRequest, IUpvestWebhookService webhookService)
    {
        using var ms = new MemoryStream();
        await httpRequest.Body.CopyToAsync(ms);
        var body = ms.ToArray();

        var headers = new Dictionary<string, string>();
        foreach (var header in httpRequest.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }

        var request = new UpvestWebhookRequest(
            httpRequest.Method,
            httpRequest.Path.Value ?? UpvestWebhooks.Path,
            httpRequest.QueryString.Value ?? string.Empty,
            headers,
            body);

        await webhookService.HandleAsync(request);

        // Always acknowledge receipt; reconciliation is the source of truth, so a processing hiccup
        // need not make Upvest retry.
        return Results.Ok();
    }
}
