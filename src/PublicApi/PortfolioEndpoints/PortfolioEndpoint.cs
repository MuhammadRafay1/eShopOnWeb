using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 5 — the caller's portfolio as Upvest currently records it.</summary>
public class GetPortfolioEndpoint : IEndpoint<IResult, GetPortfolioRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPortfolioService service, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(new GetPortfolioRequest { CallerId = http.User.ShopperId(), Cancellation = ct }, service);
            })
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(GetPortfolioRequest request, IPortfolioService service) =>
        EndpointSupport.Guarded(async () =>
        {
            var result = await service.GetAsync(request.CallerId, request.Cancellation);
            return Results.Ok(new PortfolioResponse
            {
                ModelId = result.ModelId,
                Holdings = result.Holdings
                    .Select(h => new HoldingDto { InstrumentId = h.InstrumentId, Quantity = h.Quantity, Value = h.Value })
                    .ToList(),
                TotalValue = result.TotalValue,
                PaidIn = result.PaidIn,
            });
        });
}
