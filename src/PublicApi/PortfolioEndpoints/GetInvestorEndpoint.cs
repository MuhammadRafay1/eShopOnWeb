using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Reports where the caller's investor sign-up has got to, as reflected by Upvest.</summary>
public class GetInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/investor",
            async (ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(user, async buyerId =>
                {
                    var view = await service.GetInvestorAsync(buyerId, ct);
                    return Results.Ok(new InvestorResponse(view.InvestorId, view.Status));
                }))
            .RequireJwt()
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
