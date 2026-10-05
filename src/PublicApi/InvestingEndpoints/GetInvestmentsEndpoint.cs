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
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The caller's investments, newest first.</summary>
public class GetInvestmentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IInvestingService investing, CancellationToken cancellationToken) =>
            {
                var shopperId = user.Identity?.Name;
                if (string.IsNullOrEmpty(shopperId))
                    return Results.Unauthorized();

                var investor = await investing.GetInvestorAsync(shopperId, cancellationToken);
                var investments = investor is null
                    ? new List<InvestmentDto>()
                    : investor.Investments
                        .OrderByDescending(i => i.CreatedAt)
                        .Select(i => new InvestmentDto
                        {
                            InvestmentId = i.InvestmentId,
                            Amount = i.Amount,
                            Status = i.Status.ToString().ToLowerInvariant(),
                        })
                        .ToList();

                return Results.Ok(investments);
            })
            .Produces<List<InvestmentDto>>()
            .WithTags("InvestingEndpoints");
    }
}
