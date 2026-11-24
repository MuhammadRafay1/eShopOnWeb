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

public record ChooseModelCommand(string ShopperId, string ModelId);

/// <summary>
/// Flow 2 — the signed-in investor chooses which model their portfolio follows (and may switch later).
/// </summary>
public class ChooseModelEndpoint : IEndpoint<IResult, ChooseModelCommand, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/portfolio/investor/model",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ChooseModelRequest request, ClaimsPrincipal user, IPortfolioService service) =>
            {
                var shopperId = user.GetShopperId();
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                if (request is null || string.IsNullOrWhiteSpace(request.ModelId))
                    throw new PortfolioValidationException("'modelId' is required.");
                return await HandleAsync(new ChooseModelCommand(shopperId, request.ModelId), service);
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ChooseModelCommand command, IPortfolioService service)
    {
        await service.ChooseModelAsync(command.ShopperId, command.ModelId);
        var view = await service.GetInvestorAsync(command.ShopperId);
        return view is null ? Results.NotFound() : Results.Ok(view.ToResponse());
    }
}
