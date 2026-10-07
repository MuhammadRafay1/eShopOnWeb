using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>GET /api/investing/investments — the signed-in shopper's investments, newest first.</summary>
public class InvestmentsListEndpoint : IEndpoint<IResult, HttpContext, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext http, IInvestingService investing) => await HandleAsync(http, investing))
            .Produces<InvestmentsResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http, IInvestingService investing)
    {
        var buyerId = http.User.Identity?.Name;
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investments = await investing.GetInvestmentsAsync(buyerId, http.RequestAborted);
        var response = new InvestmentsResponse
        {
            Investments = investments.Select(i => new InvestmentDto
            {
                InvestmentId = i.InvestmentId,
                Amount = i.Amount,
                Status = i.Status.ToText()
            }).ToList()
        };
        return Results.Ok(response);
    }
}
