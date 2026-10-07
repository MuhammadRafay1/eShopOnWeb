using System.Collections.Generic;
using System.Linq;
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
/// GET /api/investing/investments — the caller's investments, newest first.
/// </summary>
public class GetInvestmentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IInvestingService investingService, CancellationToken ct) =>
            {
                var buyerId = Caller.BuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Results.Unauthorized();
                }

                var investments = await investingService.GetInvestmentsAsync(buyerId, ct);
                var response = investments
                    .Select(i => new InvestmentResponse { InvestmentId = i.InvestmentId, Amount = i.Amount, Status = i.Status })
                    .ToList();
                return Results.Ok(response);
            })
            .Produces<List<InvestmentResponse>>()
            .WithTags("InvestingEndpoints");
    }
}
