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

/// <summary>Reports what the caller currently has set aside and the total they have invested so far.</summary>
public class GetBalanceEndpoint : IEndpoint<IResult, CallerRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IInvestingService investing) =>
            {
                var request = new CallerRequest { BuyerId = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty };
                return await HandleAsync(request, investing);
            })
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(CallerRequest request, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }

        var balance = await investing.GetBalanceAsync(request.BuyerId);
        var response = new BalanceResponse(request.CorrelationId())
        {
            PendingAmount = balance.PendingAmount,
            InvestedAmount = balance.InvestedAmount
        };
        return Results.Ok(response);
    }
}
