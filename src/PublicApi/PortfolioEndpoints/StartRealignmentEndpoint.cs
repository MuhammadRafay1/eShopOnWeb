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
/// Flow 4 — operator action: bring the portfolio of every investor who follows a model back to the
/// model's current weights. Restricted to the administrator role.
/// </summary>
public class StartRealignmentEndpoint : IEndpoint<IResult, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/models/{modelId}/realignments",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (string modelId, IPortfolioService service) =>
            {
                return await HandleAsync(modelId, service);
            })
            .Produces<RealignmentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string modelId, IPortfolioService service)
    {
        var view = await service.StartRealignmentAsync(modelId);
        return Results.Ok(view.ToResponse());
    }
}
