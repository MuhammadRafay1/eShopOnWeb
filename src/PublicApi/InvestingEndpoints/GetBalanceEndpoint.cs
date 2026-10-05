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

/// <summary>What the caller currently has set aside and how much has been invested so far.</summary>
public class GetBalanceEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IInvestingService investing, CancellationToken cancellationToken) =>
            {
                var shopperId = user.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId))
                    return Results.Unauthorized();

                var investor = await investing.GetInvestorAsync(shopperId, cancellationToken);
                var response = new BalanceResponse
                {
                    PendingAmount = investor?.PendingAmount ?? 0m,
                    InvestedAmount = investor?.InvestedAmount ?? 0m,
                };
                return Results.Ok(response);
            })
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }
}
