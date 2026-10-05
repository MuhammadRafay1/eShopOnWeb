using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// POST /api/investing/upvest/webhook — receives Upvest event callbacks. This is the only route
/// not driven by a shopper's token (Upvest calls it), so it is anonymous.
/// </summary>
public class UpvestWebhookEndpoint : IEndpoint<IResult, UpvestWebhookProcessor>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UpvestWebhookEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/investing/upvest/webhook",
            [AllowAnonymous] async (UpvestWebhookProcessor processor) =>
            {
                return await HandleAsync(processor);
            })
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(UpvestWebhookProcessor processor)
    {
        var http = _httpContextAccessor.HttpContext!;
        using var reader = new StreamReader(http.Request.Body);
        var body = await reader.ReadToEndAsync();
        await processor.ProcessAsync(body, http.RequestAborted);
        return Results.Ok();
    }
}
