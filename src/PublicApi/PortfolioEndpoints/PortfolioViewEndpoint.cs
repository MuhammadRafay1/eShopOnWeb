using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;
using static Microsoft.eShopWeb.PublicApi.PortfolioEndpoints.PortfolioEndpointHelpers;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 5 — the caller's portfolio as Upvest currently records it.</summary>
public class GetPortfolioEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Unauthenticated();
                }

                var result = await service.GetPortfolioAsync(buyerId, ct);
                return ToHttpResult(result, v => new PortfolioResponse(
                    v.ModelId,
                    v.Holdings.Select(h => new HoldingResponse(h.InstrumentId, h.Quantity, Money(h.Value))).ToList(),
                    Money(v.TotalValue),
                    Money(v.PaidIn)));
            })
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
