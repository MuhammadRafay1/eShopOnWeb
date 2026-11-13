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

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// GET /api/portfolio/investor — where the caller's sign-up has got to (status reflects Upvest).
/// </summary>
public class GetInvestorEndpoint : IEndpoint<IResult, CallerRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = user.Identity?.Name;
                if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();
                var request = new CallerRequest();
                request.Bind(buyerId, ct);
                return await HandleAsync(request, service);
            })
            .Produces<InvestorResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CallerRequest request, IPortfolioService service)
    {
        var investor = await service.GetInvestorAsync(request.BuyerId, request.Cancellation);
        if (investor is null) return Results.NotFound();

        return Results.Ok(new InvestorResponse
        {
            InvestorId = investor.PublicId,
            Status = StatusText.For(investor.Status),
        });
    }
}
