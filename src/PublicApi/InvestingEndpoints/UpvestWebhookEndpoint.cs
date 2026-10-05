using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Receives Upvest webhook callbacks. This is the only route Upvest itself calls, and the only one that does
/// not take a shopper token. It acknowledges deliveries; authoritative settlement is driven by signed
/// polling in the reconciliation worker, so no state is changed from this unauthenticated input.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, UpvestWebhookRequest>
{
    private readonly ILogger<UpvestWebhookEndpoint> _logger;

    public UpvestWebhookEndpoint(ILogger<UpvestWebhookEndpoint> logger) => _logger = logger;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest/webhooks",
            async (UpvestWebhookRequest request) => await HandleAsync(request))
            .AllowAnonymous()
            .WithTags("InvestingEndpoints");
    }

    public Task<IResult> HandleAsync(UpvestWebhookRequest request)
    {
        var types = request?.Payload?.Select(p => p.Type).Where(t => t is not null).ToArray() ?? System.Array.Empty<string>();
        if (types.Length > 0)
            _logger.LogInformation("Received Upvest webhook events: {EventTypes}", string.Join(", ", types));

        return Task.FromResult(Results.Ok(new { received = types.Length }));
    }
}

public class UpvestWebhookRequest
{
    public List<UpvestWebhookEvent>? Payload { get; set; }
}

public class UpvestWebhookEvent
{
    public string? Id { get; set; }
    public string? Type { get; set; }
}
