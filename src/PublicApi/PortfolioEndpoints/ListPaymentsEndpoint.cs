using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
/// Flow 3 — the caller's payments, newest first, each with its amount and status.
/// </summary>
public class ListPaymentsEndpoint : IEndpoint<IResult, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service) =>
            {
                var shopperId = user.GetShopperId();
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                return await HandleAsync(shopperId, service);
            })
            .Produces<List<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string shopperId, IPortfolioService service)
    {
        var payments = await service.GetPaymentsAsync(shopperId);
        return Results.Ok(payments.Select(p => p.ToResponse()).ToList());
    }
}
