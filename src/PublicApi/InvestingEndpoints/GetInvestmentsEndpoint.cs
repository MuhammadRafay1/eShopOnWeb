using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Investing;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Lists the signed-in shopper's investments, newest first.</summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, GetInvestmentsEndpoint.Dependencies>
{
    public record struct Dependencies(
        ClaimsPrincipal User, IInvestingService Investing, CancellationToken CancellationToken);

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IInvestingService investing,
                CancellationToken cancellationToken) =>
            {
                return await HandleAsync(new Dependencies(user, investing, cancellationToken));
            })
            .Produces<InvestmentsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(Dependencies deps)
    {
        var shopperId = deps.User.Identity?.Name;
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }

        var investor = await deps.Investing.GetLedgerAsync(shopperId, deps.CancellationToken);
        if (investor is null)
        {
            return Results.NotFound("The shopper is not enrolled.");
        }

        var response = new InvestmentsResponse
        {
            Investments = investor.Investments
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InvestmentDto
                {
                    InvestmentId = i.PublicId,
                    Amount = i.Amount,
                    Status = i.Status.ToText()
                })
                .ToList()
        };

        return Results.Ok(response);
    }
}
