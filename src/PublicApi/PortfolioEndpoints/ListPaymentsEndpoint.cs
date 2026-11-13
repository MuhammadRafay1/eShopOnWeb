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
/// Flow 3 — the caller's payments, newest first.
/// GET /api/portfolio/payments
/// </summary>
public class ListPaymentsEndpoint : IEndpoint<IResult, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service) => await HandleAsync(user.BuyerId(), service))
            .Produces<PaymentResponse[]>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IPortfolioService service)
    {
        var payments = await service.ListPaymentsAsync(buyerId, default);
        var response = payments
            .Select(p => new PaymentResponse(p.PaymentId, PortfolioEndpointHelpers.Money(p.Amount), p.Status))
            .ToList();
        return Results.Ok(response);
    }
}
