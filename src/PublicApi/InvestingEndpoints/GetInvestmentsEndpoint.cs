using System.Linq;
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

/// <summary>
/// GET api/investing/investments — the caller's investments, newest first.
/// </summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetInvestmentsEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService investingService) => await HandleAsync(investingService))
            .Produces<InvestmentsResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService investingService)
    {
        var buyerId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        var investments = await investingService.GetInvestmentsAsync(buyerId, CancellationToken.None);
        return Results.Ok(new InvestmentsResponse
        {
            Investments = investments
                .Select(i => new InvestmentDto { InvestmentId = i.InvestmentId, Amount = i.Amount, Status = i.Status })
                .ToList()
        });
    }
}
