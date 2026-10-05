using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.eShopWeb.Infrastructure.Upvest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives order/execution webhooks from Upvest. This is the only route Upvest itself calls, and
/// the only one that does not require a shopper token. Every webhook is signature-verified against
/// Upvest's JWKS before being acted on. Settlement is also guaranteed independently by the polling
/// reconciler, so this endpoint is a timeliness supplement rather than the sole mechanism.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, EmptyInvestingRequest, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UpvestWebhookEndpoint> _logger;

    public UpvestWebhookEndpoint(IHttpContextAccessor httpContextAccessor, ILogger<UpvestWebhookEndpoint> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest-events",
            async (IInvestingService investingService) => await HandleAsync(new EmptyInvestingRequest(), investingService))
            .AllowAnonymous()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(EmptyInvestingRequest request, IInvestingService investingService)
    {
        var http = _httpContextAccessor.HttpContext;
        if (http is null) return Results.StatusCode(StatusCodes.Status500InternalServerError);

        byte[] body;
        using (var ms = new MemoryStream())
        {
            await http.Request.Body.CopyToAsync(ms);
            body = ms.ToArray();
        }

        // Verify the Upvest signature against the current JWKS before acting on anything.
        try
        {
            var upvest = http.RequestServices.GetRequiredService<IUpvestClient>();
            var keys = await upvest.GetVerificationKeysAsync(http.RequestAborted);
            var verified = UpvestWebhookVerifier.Verify(
                http.Request.Method,
                http.Request.Path.Value ?? string.Empty,
                http.Request.QueryString.Value ?? string.Empty,
                name => http.Request.Headers.TryGetValue(name, out var v) ? v.ToString() : null,
                keys);

            if (!verified)
            {
                _logger.LogWarning("Rejected an Upvest webhook with an invalid signature.");
                return Results.Unauthorized();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify Upvest webhook signature.");
            return Results.Unauthorized();
        }

        if (TryExtractOrderOutcome(body, out var orderId, out var orderStatus) && orderId is not null)
        {
            await investingService.SettleInvestmentByUpvestOrderAsync(orderId, orderStatus!, http.RequestAborted);
        }

        return Results.Ok();
    }

    /// <summary>
    /// Maps an order/execution webhook event to the affected order id and a normalized order status.
    /// </summary>
    private static bool TryExtractOrderOutcome(byte[] body, out string? orderId, out string? orderStatus)
    {
        orderId = null;
        orderStatus = null;
        if (body.Length == 0) return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeElement)) return false;
            var type = typeElement.GetString();
            if (string.IsNullOrEmpty(type) || !root.TryGetProperty("object", out var obj)) return false;

            switch (type)
            {
                case "ORDER.FILLED":
                    orderStatus = UpvestStatuses.OrderFilled;
                    orderId = obj.TryGetProperty("id", out var oid) ? oid.GetString() : null;
                    return orderId is not null;
                case "ORDER.CANCELLED":
                    orderStatus = UpvestStatuses.OrderCancelled;
                    orderId = obj.TryGetProperty("id", out var cid) ? cid.GetString() : null;
                    return orderId is not null;
                case "EXECUTION.FILLED":
                case "EXECUTION.SETTLED":
                    orderStatus = UpvestStatuses.OrderFilled;
                    orderId = obj.TryGetProperty("order_id", out var eoid) ? eoid.GetString() : null;
                    return orderId is not null;
                case "EXECUTION.CANCELLED":
                    orderStatus = UpvestStatuses.OrderCancelled;
                    orderId = obj.TryGetProperty("order_id", out var ecid) ? ecid.GetString() : null;
                    return orderId is not null;
                default:
                    return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
