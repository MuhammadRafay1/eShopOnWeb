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
/// Flow 1 — reports where the caller's investor sign-up has got to.
/// </summary>
public class GetInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var shopperId = CallerIdentity.GetShopperId(user);
                if (string.IsNullOrEmpty(shopperId))
                {
                    return Results.Unauthorized();
                }

                var result = await service.GetInvestorAsync(shopperId, ct);
                if (result is null)
                {
                    return Results.NotFound(new { message = "You have not signed up as an investor yet." });
                }

                return Results.Ok(new { investorId = result.InvestorId, status = result.Status });
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }
}
