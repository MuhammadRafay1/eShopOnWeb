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
/// Flow 2 — list every model portfolio and its funds.
/// GET /api/portfolio/models
/// </summary>
public class ListModelsEndpoint : IEndpoint<IResult, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/models",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPortfolioService service) => await HandleAsync(service))
            .Produces<ModelResponse[]>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IPortfolioService service)
    {
        var models = await service.ListModelsAsync(default);
        var response = models
            .Select(m => new ModelResponse(
                m.ModelId,
                m.Name,
                m.Funds.Select(f => new FundResponse(f.InstrumentId, f.Weight)).ToList()))
            .ToList();
        return Results.Ok(response);
    }
}
