using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.InvestingAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>
/// Shows what the signed-in shopper currently has set aside and the total they
/// have invested so far.
/// </summary>
public class BalanceGetEndpoint : IEndpoint<IResult, IReadRepository<Investor>>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BalanceGetEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IReadRepository<Investor> investors) =>
            {
                return await HandleAsync(investors);
            })
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IReadRepository<Investor> investors)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var ct = httpContext.RequestAborted;
        var buyerId = httpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investor = await investors.FirstOrDefaultAsync(new InvestorByBuyerIdSpecification(buyerId), ct);

        return Results.Ok(new BalanceResponse
        {
            PendingAmount = investor?.PendingAmount ?? 0m,
            InvestedAmount = investor?.TotalInvested() ?? 0m
        });
    }
}
