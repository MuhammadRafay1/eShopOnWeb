using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Investing;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The caller's investments, newest first (GET /api/investing/investments).</summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, CancellationToken cancellationToken) => await HandleAsync(http))
            .Produces<IReadOnlyList<InvestmentResponse>>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext http)
    {
        var shopperId = http.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var service = http.RequestServices.GetRequiredService<IInvestingService>();
        var investments = await service.GetInvestmentsAsync(shopperId, http.RequestAborted);

        var response = investments.Select(i => new InvestmentResponse
        {
            InvestmentId = i.Id,
            Amount = i.Amount,
            Status = InvestingStatusText.For(i.Status),
        }).ToList();

        return Results.Ok(response);
    }
}
