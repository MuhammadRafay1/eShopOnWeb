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
/// Flow 3 — the signed-in investor pays an amount in (euros). The money is credited at Upvest
/// and invested across the funds of the model they follow.
/// </summary>
public class CreatePaymentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentRequest request, ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var shopperId = CallerIdentity.GetShopperId(user);
                if (string.IsNullOrEmpty(shopperId))
                {
                    return Results.Unauthorized();
                }

                var result = await service.CreatePaymentAsync(shopperId, request.Amount, ct);
                return result.Status switch
                {
                    PortfolioOperationStatus.Ok => Results.Ok(new
                    {
                        paymentId = result.Value!.PaymentId,
                        status = result.Value.Status
                    }),
                    PortfolioOperationStatus.NotEligible => Results.Conflict(new { message = result.Message }),
                    _ => Results.BadRequest(new { message = result.Message })
                };
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }
}
