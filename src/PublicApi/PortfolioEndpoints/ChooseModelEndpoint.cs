using System.Security.Claims;
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
/// Flow 2 — the signed-in investor chooses which model their portfolio follows (and may switch later).
/// </summary>
public class ChooseModelEndpoint : IEndpoint<IResult, ChooseModelRequest, ClaimsPrincipal, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ChooseModelRequest request, ClaimsPrincipal user, IPortfolioService service) =>
                await HandleAsync(request, user, service))
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ChooseModelRequest request, ClaimsPrincipal user, IPortfolioService service)
    {
        var shopperId = PortfolioCaller.ShopperId(user);
        await service.ChooseModelAsync(shopperId, request.ModelId);
        return Results.NoContent();
    }
}
