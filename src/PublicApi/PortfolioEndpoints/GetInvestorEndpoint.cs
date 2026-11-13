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
/// Flow 1 — where the caller's investor sign-up has got to, as recorded at Upvest.
/// GET /api/portfolio/investor
/// </summary>
public class GetInvestorEndpoint : IEndpoint<IResult, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service) => await HandleAsync(user.BuyerId(), service))
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IPortfolioService service)
    {
        var view = await service.GetInvestorAsync(buyerId, default);
        return view is null
            ? PortfolioEndpointHelpers.NotFoundResult("This shopper is not signed up as an investor.")
            : Results.Ok(new InvestorResponse(view.InvestorId, view.Status));
    }
}
