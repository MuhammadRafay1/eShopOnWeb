using System;
using System.Security.Claims;
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
/// What the signed-in shopper currently has set aside and has invested so far
/// (GET api/investing/balance).
/// </summary>
public class GetBalanceEndpoint : IEndpoint<IResult, string, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IInvestingService investingService) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name);
                return string.IsNullOrEmpty(buyerId) ? Results.Unauthorized() : await HandleAsync(buyerId, investingService);
            })
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IInvestingService investingService)
    {
        var balance = await investingService.GetBalanceAsync(buyerId);
        if (balance is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new BalanceResponse
        {
            PendingAmount = decimal.Round(balance.PendingAmount, 2),
            InvestedAmount = decimal.Round(balance.InvestedAmount, 2),
        });
    }
}

public class BalanceResponse : BaseResponse
{
    public BalanceResponse(Guid correlationId) : base(correlationId) { }

    public BalanceResponse() { }

    /// <summary>Change set aside and not yet invested, in euros.</summary>
    public decimal PendingAmount { get; set; }

    /// <summary>Total invested on the shopper's behalf so far, in euros.</summary>
    public decimal InvestedAmount { get; set; }
}
