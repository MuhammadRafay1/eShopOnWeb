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

/// <summary>GET /api/portfolio — the caller's portfolio as Upvest currently records it.</summary>
public class PortfolioQueryEndpoint : IEndpoint<IResult, IPortfolioQueryService>
{
    private readonly IHttpContextAccessor _http;
    public PortfolioQueryEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPortfolioQueryService service) => await HandleAsync(service))
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IPortfolioQueryService service)
    {
        var ctx = _http.HttpContext!;
        var userName = PortfolioHttp.UserName(ctx);
        if (string.IsNullOrEmpty(userName))
        {
            return Results.Unauthorized();
        }

        var view = await service.GetPortfolioAsync(userName, ctx.RequestAborted);
        if (view is null)
        {
            return Results.NotFound(new { message = "You are not signed up as an investor." });
        }

        return Results.Ok(new PortfolioResponse
        {
            ModelId = view.ModelId,
            Holdings = view.Holdings.Select(h => new HoldingDto
            {
                InstrumentId = h.InstrumentId,
                Quantity = h.Quantity,
                Value = PortfolioHttp.Money(h.Value)
            }).ToList(),
            TotalValue = PortfolioHttp.Money(view.TotalValue),
            PaidIn = PortfolioHttp.Money(view.PaidIn)
        });
    }
}
