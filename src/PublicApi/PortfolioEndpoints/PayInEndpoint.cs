using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>The signed-in investor pays an amount in (in euros).</summary>
public class PayInEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            async (PayInRequest request, ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(user, async buyerId =>
                {
                    var view = await service.PayInAsync(buyerId, request.Amount, ct);
                    return Results.Ok(new PaymentResponse(view.PaymentId, decimal.Round(view.Amount, 2), view.Status));
                }))
            .RequireJwt()
            .Produces<PaymentResponse>()
            .WithTags("PortfolioEndpoints");
    }
}
