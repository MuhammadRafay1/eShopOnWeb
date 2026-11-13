using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Lists every model portfolio and its funds.</summary>
public class GetModelsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            async (IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(async () =>
                {
                    var views = await service.GetModelsAsync(ct);
                    return Results.Ok(views.Select(UpsertModelEndpoint.ToResponse).ToList());
                }))
            .RequireJwt()
            .Produces<System.Collections.Generic.IReadOnlyList<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}
