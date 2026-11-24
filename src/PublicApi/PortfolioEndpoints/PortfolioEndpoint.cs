using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>The caller's portfolio as Upvest currently records it.</summary>
public class GetPortfolioEndpoint : IEndpoint<IResult, IManagedPortfolioService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetPortfolioEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IManagedPortfolioService service) => await HandleAsync(service))
            .Produces<PortfolioResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IManagedPortfolioService service)
    {
        var shopperId = PortfolioMapping.ShopperId(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }

        var portfolio = await service.GetPortfolioAsync(shopperId);
        if (portfolio is null)
        {
            return Results.NotFound("The caller has not signed up as an investor.");
        }

        var response = new PortfolioResponse(
            portfolio.ModelId,
            portfolio.Holdings.Select(h => new HoldingResponse(h.InstrumentId, h.Quantity, PortfolioMapping.Money(h.Value))).ToList(),
            PortfolioMapping.Money(portfolio.TotalValue),
            PortfolioMapping.Money(portfolio.PaidIn));

        return Results.Ok(response);
    }
}
