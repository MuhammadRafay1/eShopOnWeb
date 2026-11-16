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
/// Flow 2 — operator action: create or replace a model portfolio. Administrators only.
/// </summary>
public class UpsertModelEndpoint : IEndpoint<IResult, UpsertModelRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/models/{modelId:int}",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int modelId, UpsertModelRequest request, IPortfolioService service) =>
                await HandleAsync(modelId, request, service))
            .Produces<ApplicationCore.Portfolio.ModelView>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(UpsertModelRequest request, IPortfolioService service) =>
        HandleAsync(0, request, service);

    private static async Task<IResult> HandleAsync(int modelId, UpsertModelRequest request, IPortfolioService service)
    {
        var (name, funds) = request.ToDomain();
        var view = await service.UpsertModelAsync(modelId, name, funds);
        return Results.Ok(view);
    }
}
