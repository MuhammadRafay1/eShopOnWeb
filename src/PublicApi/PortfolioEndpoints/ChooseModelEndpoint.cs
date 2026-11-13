using System.Security.Claims;
using System.Threading;
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
/// PUT /api/portfolio/investor/model — the signed-in investor chooses which model their portfolio follows.
/// </summary>
public class ChooseModelEndpoint : IEndpoint<IResult, ChooseModelRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ChooseModelRequest request, ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = user.Identity?.Name;
                if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();
                request.Bind(buyerId, ct);
                return await HandleAsync(request, service);
            })
            .Produces(StatusCodes.Status200OK)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ChooseModelRequest request, IPortfolioService service)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw new PortfolioValidationException("modelId is required.");
        }

        await service.ChooseModelAsync(request.BuyerId, request.ModelId.Trim(), request.Cancellation);
        return Results.Ok(new { modelId = request.ModelId.Trim() });
    }
}
