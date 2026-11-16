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
/// Flow 4 — operator action: realign every investor following a model to its current weights. Administrators only.
/// </summary>
public class RealignEndpoint : IEndpoint<IResult, int, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId:int}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int modelId, IPortfolioService service) => await HandleAsync(modelId, service))
            .Produces<ApplicationCore.Portfolio.RealignmentView>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(int modelId, IPortfolioService service)
    {
        var view = await service.RealignAsync(modelId);
        return Results.Accepted($"api/portfolio/realignments/{view.RealignmentId}", view);
    }
}
