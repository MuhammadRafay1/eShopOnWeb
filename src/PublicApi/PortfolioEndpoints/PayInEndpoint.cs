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
/// Flow 3 — the signed-in investor pays an amount (euros) into their managed portfolio.
/// </summary>
public class PayInEndpoint : IEndpoint<IResult, PayInRequest, ClaimsPrincipal, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PayInRequest request, ClaimsPrincipal user, IPortfolioService service) =>
                await HandleAsync(request, user, service))
            .Produces<ApplicationCore.Portfolio.PaymentView>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(PayInRequest request, ClaimsPrincipal user, IPortfolioService service)
    {
        var shopperId = PortfolioCaller.ShopperId(user);
        var view = await service.PayInAsync(shopperId, request.Amount);
        return Results.Ok(view);
    }
}
