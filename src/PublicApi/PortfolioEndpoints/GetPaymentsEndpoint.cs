using System.Linq;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Portfolio;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>The caller's payments, newest first.</summary>
public class GetPaymentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            async (ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
                await PortfolioEndpointHelpers.RunAsync(user, async buyerId =>
                {
                    var views = await service.GetPaymentsAsync(buyerId, ct);
                    var response = views
                        .Select(v => new PaymentResponse(v.PaymentId, decimal.Round(v.Amount, 2), v.Status))
                        .ToList();
                    return Results.Ok(response);
                }))
            .RequireJwt()
            .Produces<System.Collections.Generic.IReadOnlyList<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}
