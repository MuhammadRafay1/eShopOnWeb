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
/// Flow 1 — signs the signed-in shopper up as an investor with Upvest.
/// </summary>
public class SignUpInvestorEndpoint : IEndpoint<IResult, SignUpInvestorRequest, ClaimsPrincipal, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (SignUpInvestorRequest request, ClaimsPrincipal user, IPortfolioService service) =>
                await HandleAsync(request, user, service))
            .Produces<ApplicationCore.Portfolio.InvestorView>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(SignUpInvestorRequest request, ClaimsPrincipal user, IPortfolioService service)
    {
        var shopperId = PortfolioCaller.ShopperId(user);
        var signUp = request.ToDomain();
        var view = await service.SignUpInvestorAsync(shopperId, signUp);
        return Results.Ok(view);
    }
}
