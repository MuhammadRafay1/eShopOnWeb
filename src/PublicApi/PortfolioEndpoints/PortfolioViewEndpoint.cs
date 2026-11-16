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

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 5 — GET /api/portfolio: the caller's portfolio as Upvest currently records it.</summary>
public class GetPortfolioEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IPortfolioViewService service,
                CancellationToken ct) =>
            {
                var userName = PortfolioEndpointHelpers.GetUserName(user);
                if (string.IsNullOrEmpty(userName)) return Results.Unauthorized();

                var view = await service.GetAsync(userName, ct);
                if (view is null)
                    return Results.NotFound(new { message = "You have not signed up as an investor yet." });

                return Results.Ok(new PortfolioResponse
                {
                    ModelId = view.ModelId,
                    Holdings = view.Holdings
                        .Select(h => new HoldingDto { InstrumentId = h.InstrumentId, Quantity = h.Quantity, Value = h.Value })
                        .ToList(),
                    TotalValue = view.TotalValue,
                    PaidIn = view.PaidIn
                });
            })
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
