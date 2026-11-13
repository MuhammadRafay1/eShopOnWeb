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

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>
/// Flow 3 — the caller's payments, newest first.
/// </summary>
public class ListPaymentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var shopperId = CallerIdentity.GetShopperId(user);
                if (string.IsNullOrEmpty(shopperId))
                {
                    return Results.Unauthorized();
                }

                var payments = await service.ListPaymentsAsync(shopperId, ct);
                return Results.Ok(payments.Select(p => new
                {
                    paymentId = p.PaymentId,
                    amount = p.Amount,
                    status = p.Status
                }));
            })
            .Produces<object>()
            .WithTags("PortfolioEndpoints");
    }
}
