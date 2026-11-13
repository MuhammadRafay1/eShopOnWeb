using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Signs the signed-in shopper up as an investor with Upvest.</summary>
public class SignUpInvestorEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/investor",
            async (InvestorSignupRequest request, ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(user, async buyerId =>
                {
                    var registration = PortfolioRequestMapper.ToRegistration(request);
                    var view = await service.SignUpInvestorAsync(buyerId, registration, ct);
                    return Results.Ok(new InvestorResponse(view.InvestorId, view.Status));
                }))
            .RequireJwt()
            .Produces<InvestorResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
