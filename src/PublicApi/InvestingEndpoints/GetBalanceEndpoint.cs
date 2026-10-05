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

/// <summary>Reports what the caller currently has set aside and the total amount invested so far.</summary>
public class GetBalanceEndpoint : IEndpoint<IResult, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
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
        var balance = await investing.GetBalanceAsync(shopper, http.RequestAborted);
        return Results.Ok(new BalanceResponse
        {
            PendingAmount = balance.PendingAmount,
            InvestedAmount = balance.InvestedAmount,
        });
    }
}
