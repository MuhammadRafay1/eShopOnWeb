using System;
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
/// Flow 4 — where a realignment has got to.
/// </summary>
public class GetRealignmentEndpoint : IEndpoint<IResult, Guid, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/realignments/{realignmentId:guid}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (Guid realignmentId, IPortfolioService service) => await HandleAsync(realignmentId, service))
            .Produces<ApplicationCore.Portfolio.RealignmentView>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(Guid realignmentId, IPortfolioService service)
    {
        var view = await service.GetRealignmentAsync(realignmentId);
        return Results.Ok(view);
    }
}
