using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives Upvest webhook deliveries. This is the only route Upvest itself calls, so it is not
/// JWT-authenticated; instead each delivery's HTTP Message Signature is verified against Upvest's
/// public key, and the body is checked against its digest. Events are applied idempotently; the
/// background reconciler remains the authoritative settlement path.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost(UpvestWebhookRegistrar.WebhookPath,
            [AllowAnonymous] async (HttpContext context, System.Security.Claims.ClaimsPrincipal user) => await HandleAsync(context))
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext context)
    {
        var verifier = context.RequestServices.GetRequiredService<IUpvestWebhookVerifier>();
        var processor = context.RequestServices.GetRequiredService<IUpvestWebhookProcessor>();
        var request = context.Request;

        string body;
        using (var reader = new StreamReader(request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        var headers = request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value.ToString());

        if (!await verifier.VerifyAsync(request.Method, request.Path, request.QueryString.Value ?? string.Empty, headers, context.RequestAborted))
        {
            return Results.Unauthorized();
        }
        if (!DigestMatches(headers, body))
        {
            return Results.Unauthorized();
        }

        foreach (var (type, element) in ParseEvents(body))
        {
            await processor.ProcessAsync(type, element, context.RequestAborted);
        }

        return Results.Ok();
    }

    private static bool DigestMatches(IReadOnlyDictionary<string, string> headers, string body)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        if (headers.TryGetValue("digest", out var digest) && digest.StartsWith("SHA-256=", StringComparison.OrdinalIgnoreCase))
        {
            var expected = Convert.ToBase64String(SHA256.HashData(bytes));
            return digest["SHA-256=".Length..].Trim() == expected;
        }
        if (headers.TryGetValue("content-digest", out var cd) && cd.Contains("sha-512="))
        {
            var start = cd.IndexOf(':') + 1;
            var end = cd.LastIndexOf(':');
            if (start > 0 && end > start)
            {
                var expected = Convert.ToBase64String(SHA512.HashData(bytes));
                return cd[start..end] == expected;
            }
        }
        // No digest header present: nothing to cross-check (signature already verified).
        return true;
    }

    private static IEnumerable<(string Type, JsonElement Object)> ParseEvents(string body)
    {
        var results = new List<(string, JsonElement)>();
        if (string.IsNullOrWhiteSpace(body)) return results;

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement.Clone();

        void AddEvent(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return;
            var type = e.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(type)) return;
            var obj = e.TryGetProperty("object", out var o) ? o : default;
            results.Add((type!, obj));
        }

        // Upvest batches events as {"payload": [ {type, object}, ... ]}; also tolerate a single event.
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in payload.EnumerateArray()) AddEvent(e);
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in root.EnumerateArray()) AddEvent(e);
        }
        else
        {
            AddEvent(root);
        }
        return results;
    }
}
