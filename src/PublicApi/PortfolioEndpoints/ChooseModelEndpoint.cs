using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>The signed-in investor chooses which model their portfolio follows.</summary>
public class ChooseModelEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            async (ChooseModelRequest request, ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(user, async buyerId =>
                {
                    if (string.IsNullOrWhiteSpace(request.ModelId))
                        return Results.Problem("modelId is required.", statusCode: StatusCodes.Status400BadRequest);

                    await service.ChooseModelAsync(buyerId, request.ModelId, ct);
                    return Results.Ok(new { modelId = request.ModelId });
                }))
            .RequireJwt()
            .WithTags("PortfolioEndpoints");
    }
}
