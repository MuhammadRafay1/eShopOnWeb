using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives order/execution webhooks from Upvest and reflects their outcome onto the matching
/// investment. This is the only route Upvest itself calls; it carries no shopper token and is
/// authenticated instead by verifying Upvest's HTTP message signature. Settlement is also
/// reconciled by polling, so this endpoint is a real-time optimisation rather than the sole path.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, HttpContext>
{
    private static readonly HashSet<string> OrderStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "NEW", "PROCESSING", "FILLED", "CANCELLED" };

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest/webhook",
            async (HttpContext http, CancellationToken _) => await HandleAsync(http))
            .AllowAnonymous()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http)
    {
        var services = http.RequestServices;
        var investing = services.GetRequiredService<IInvestingService>();
        var verifier = services.GetRequiredService<UpvestWebhookVerifier>();
        var logger = services.GetRequiredService<ILogger<UpvestWebhookEndpoint>>();

        var request = http.Request;
        byte[] body;
        using (var ms = new MemoryStream())
        {
            await request.Body.CopyToAsync(ms, http.RequestAborted);
            body = ms.ToArray();
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
            headers[header.Key] = header.Value.ToString();

        var valid = await verifier.VerifyAsync(
            request.Method, request.Path.Value ?? "/", request.QueryString.Value ?? string.Empty,
            request.Host.Value ?? string.Empty, headers, body, http.RequestAborted);

        if (!valid)
        {
            logger.LogWarning("Rejected an Upvest webhook with an invalid or missing signature.");
            return Results.Unauthorized();
        }

        try
        {
            var outcomes = new List<(string Id, string Status)>();
            using (var doc = JsonDocument.Parse(body))
                CollectOrderOutcomes(doc.RootElement, outcomes);

            foreach (var (id, status) in outcomes)
                await investing.ApplyOrderOutcomeAsync(id, status, http.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to apply an Upvest webhook payload: {Error}", ex.Message);
        }

        return Results.Ok(new { success = true });
    }

    /// <summary>Walks the payload for any object carrying an order id and an order status.</summary>
    private static void CollectOrderOutcomes(JsonElement element, List<(string, string)> outcomes)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String &&
                    element.TryGetProperty("status", out var statusEl) && statusEl.ValueKind == JsonValueKind.String &&
                    OrderStatuses.Contains(statusEl.GetString()!))
                {
                    outcomes.Add((idEl.GetString()!, statusEl.GetString()!));
                }
                foreach (var property in element.EnumerateObject())
                    CollectOrderOutcomes(property.Value, outcomes);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    CollectOrderOutcomes(item, outcomes);
                break;
        }
    }
}
