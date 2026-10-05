using System.Linq;
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
/// Lists the signed-in shopper's investments, newest first.
/// </summary>
public class InvestmentsGetEndpoint : IEndpoint<IResult, IReadRepository<Investor>>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InvestmentsGetEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IReadRepository<Investor> investors) =>
            {
                return await HandleAsync(investors);
            })
            .Produces<InvestmentsResponse>()
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

        var response = new InvestmentsResponse();
        if (investor is not null)
        {
            response.Investments = investor.Investments
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InvestmentResponse
                {
                    InvestmentId = i.Id,
                    Amount = i.Amount,
                    Status = i.Status.ToString().ToLowerInvariant()
                })
                .ToList();
        }

        return Results.Ok(response);
    }
}
