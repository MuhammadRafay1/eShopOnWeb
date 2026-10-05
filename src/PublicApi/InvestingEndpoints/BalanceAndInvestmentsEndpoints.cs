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

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>GET /api/investing/balance — what the caller has set aside and the total invested so far.</summary>
public class GetBalanceEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IInvestingService investingService,
                CancellationToken cancellationToken) =>
            {
                var buyerId = CallerIdentity.GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                var balance = await investingService.GetBalanceAsync(buyerId, cancellationToken);
                return balance is null
                    ? Results.NotFound()
                    : Results.Ok(new BalanceResponse { PendingAmount = balance.PendingAmount, InvestedAmount = balance.InvestedAmount });
            })
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }
}

/// <summary>GET /api/investing/investments — the caller's investments, newest first.</summary>
public class ListInvestmentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IInvestingService investingService,
                CancellationToken cancellationToken) =>
            {
                var buyerId = CallerIdentity.GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                var investments = await investingService.GetInvestmentsAsync(buyerId, cancellationToken);
                var response = investments
                    .Select(i => new InvestmentResponse { InvestmentId = i.InvestmentId, Amount = i.Amount, Status = i.Status })
                    .ToList();
                return Results.Ok(response);
            })
            .Produces<System.Collections.Generic.List<InvestmentResponse>>()
            .WithTags("InvestingEndpoints");
    }
}
