using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>GET /api/investing/balance — the signed-in shopper's set-aside and invested balances.</summary>
public class BalanceGetEndpoint : IEndpoint<IResult, HttpContext, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext http, IInvestingService investing) => await HandleAsync(http, investing))
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http, IInvestingService investing)
    {
        var buyerId = http.User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var balance = await investing.GetBalanceAsync(buyerId, http.RequestAborted);
        return Results.Ok(new BalanceResponse
        {
            PendingAmount = balance.PendingAmount,
            InvestedAmount = balance.InvestedAmount
        });
    }
}
