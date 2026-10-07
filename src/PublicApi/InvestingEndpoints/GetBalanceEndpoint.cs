using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// GET /api/investing/balance — what the caller has set aside and invested so far.
/// </summary>
public class GetBalanceEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IInvestingService investingService, CancellationToken ct) =>
            {
                var buyerId = Caller.BuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                var balance = await investingService.GetBalanceAsync(buyerId, ct);
                return Results.Ok(new BalanceResponse
                {
                    PendingAmount = balance.PendingAmount,
                    InvestedAmount = balance.InvestedAmount
                });
            })
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }
}
