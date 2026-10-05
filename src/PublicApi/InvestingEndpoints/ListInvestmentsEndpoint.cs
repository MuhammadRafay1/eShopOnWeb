using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Lists the caller's investments, newest first.</summary>
public class ListInvestmentsEndpoint : IEndpoint<IResult, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, CancellationToken _) => await HandleAsync(http))
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http)
    {
        var shopper = InvestingContractMapping.ResolveShopper(http);
        if (string.IsNullOrEmpty(shopper))
            return Results.Unauthorized();

        var investing = http.RequestServices.GetRequiredService<IInvestingService>();
        var investments = await investing.GetInvestmentsAsync(shopper, http.RequestAborted);
        var response = investments.Select(i => new InvestmentResponse
        {
            InvestmentId = i.InvestmentId,
            Amount = i.Amount,
            Status = InvestingContractMapping.ToApi(i.Status),
        }).ToArray();

        return Results.Ok(response);
    }
}
