using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 2 — the signed-in investor chooses which model their portfolio follows.
/// PUT /api/portfolio/investor/model
/// </summary>
public class SelectModelEndpoint : IEndpoint<IResult, SelectModelRequest, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SelectModelRequest request, ClaimsPrincipal user, IPortfolioService service) =>
                await HandleAsync(request, user.BuyerId(), service))
            .Produces(StatusCodes.Status204NoContent)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(SelectModelRequest request, string buyerId, IPortfolioService service)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw PortfolioException.BadRequest("A modelId is required.");
        }

        await service.SelectModelAsync(buyerId, request.ModelId, default);
        return Results.NoContent();
    }
}
