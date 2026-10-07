using System.Linq;
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

/// <summary>Lists the caller's investments, newest first, each reconciled against Upvest.</summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, CallerRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IInvestingService investing) =>
            {
                var request = new CallerRequest { BuyerId = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty };
                return await HandleAsync(request, investing);
            })
            .Produces<InvestmentsResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(CallerRequest request, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }

        var investments = await investing.ListInvestmentsAsync(request.BuyerId);
        var response = new InvestmentsResponse(request.CorrelationId())
        {
            Investments = investments.Select(i => new InvestmentDto
            {
                InvestmentId = i.Id,
                Amount = i.Amount,
                Status = i.Status.ToApiString()
            }).ToList()
        };
        return Results.Ok(response);
    }
}
