using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 2 — every model portfolio and its funds. Available to any signed-in shopper.
/// </summary>
public class ListModelsEndpoint : IEndpoint<IResult, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPortfolioService service) =>
            {
                return await HandleAsync(service);
            })
            .Produces<List<ModelResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IPortfolioService service)
    {
        var models = await service.GetModelsAsync();
        return Results.Ok(models.Select(m => m.ToResponse()).ToList());
    }
}
