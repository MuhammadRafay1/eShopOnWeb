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

/// <summary>
/// Flow 5 — the caller's portfolio as Upvest currently records it: the model it follows, each
/// fund held with its quantity and current value, the total current value, and the total paid in.
/// </summary>
public class GetPortfolioEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var shopperId = CallerIdentity.GetShopperId(user);
                if (string.IsNullOrEmpty(shopperId))
                {
                    return Results.Unauthorized();
                }

                var portfolio = await service.GetPortfolioAsync(shopperId, ct);
                if (portfolio is null)
                {
                    return Results.NotFound(new { message = "You have not signed up as an investor yet." });
                }

                return Results.Ok(new
                {
                    modelId = portfolio.ModelId,
                    holdings = portfolio.Holdings.Select(h => new
                    {
                        instrumentId = h.InstrumentId,
                        quantity = h.Quantity,
                        value = h.Value
                    }),
                    totalValue = portfolio.TotalValue,
                    paidIn = portfolio.PaidIn
                });
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }
}
