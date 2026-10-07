using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// GET /api/investing/balance — what the caller currently has set aside and not yet invested, and
/// the total amount invested so far.
/// </summary>
public class GetBalanceEndpoint : IEndpoint<IResult, ShopperScopedRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService service, HttpContext http) =>
            {
                var shopperId = InvestingShared.ShopperId(http.User);
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                return await HandleAsync(new ShopperScopedRequest(shopperId), service);
            })
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(ShopperScopedRequest request, IInvestingService service)
    {
        var balance = await service.GetBalanceAsync(request.ShopperId);
        return Results.Ok(new BalanceResponse(balance.PendingAmount, balance.InvestedAmount));
    }
}

public record BalanceResponse(decimal PendingAmount, decimal InvestedAmount);
