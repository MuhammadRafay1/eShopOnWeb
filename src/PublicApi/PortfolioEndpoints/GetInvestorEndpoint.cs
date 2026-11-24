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
/// Flow 1 — where the caller's investor sign-up has got to.
/// </summary>
public class GetInvestorEndpoint : IEndpoint<IResult, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service) =>
            {
                var shopperId = user.GetShopperId();
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                return await HandleAsync(shopperId, service);
            })
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string shopperId, IPortfolioService service)
    {
        var view = await service.GetInvestorAsync(shopperId);
        return view is null
            ? Results.NotFound()
            : Results.Ok(view.ToResponse());
    }
}
