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

/// <summary>
/// GET /api/investing/investments — the caller's investments, newest first.
/// </summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, HttpContext, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext http, IInvestingService service) => await HandleAsync(http, service))
            .Produces<InvestmentResponse[]>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http, IInvestingService service)
    {
        var buyerId = InvestingEndpointHelpers.BuyerId(http.User);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investments = await service.GetInvestmentsAsync(buyerId, http.RequestAborted);

        var response = investments.Investments
            .Select(i => new InvestmentResponse
            {
                InvestmentId = i.InvestmentId,
                Amount = i.Amount,
                Status = i.Status.Wire()
            })
            .ToArray();

        return Results.Ok(response);
    }
}
