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
/// Flow 3 — the caller's payments, newest first.
/// </summary>
public class ListPaymentsEndpoint : IEndpoint<IResult, ClaimsPrincipal, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service) =>
                await HandleAsync(user, service))
            .Produces<System.Collections.Generic.IReadOnlyList<ApplicationCore.Portfolio.PaymentView>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ClaimsPrincipal user, IPortfolioService service)
    {
        var shopperId = PortfolioCaller.ShopperId(user);
        var payments = await service.GetPaymentsAsync(shopperId);
        return Results.Ok(payments);
    }
}
