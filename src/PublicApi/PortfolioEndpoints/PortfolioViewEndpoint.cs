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

public sealed class GetPortfolioRequest : ShopperRequest { }

/// <summary>
/// GET /api/portfolio — the caller's portfolio as Upvest currently records it: the model it
/// follows, each fund held with its quantity and current value, the total value, and the total
/// amount paid in.
/// </summary>
public sealed class GetPortfolioEndpoint : IEndpoint<IResult, GetPortfolioRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, IPortfolioService service) =>
                await HandleAsync(new GetPortfolioRequest
                {
                    ShopperId = PortfolioEndpointHelpers.ShopperId(http),
                    CancellationToken = http.RequestAborted
                }, service))
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(GetPortfolioRequest request, IPortfolioService service)
    {
        var view = await service.GetPortfolioAsync(request.ShopperId, request.CancellationToken);
        var response = new PortfolioResponse(
            view.ModelId,
            view.Holdings.Select(h => new HoldingResponse(h.InstrumentId, h.Quantity, h.Value)).ToList(),
            view.TotalValue,
            view.PaidIn);
        return Results.Ok(response);
    }
}
