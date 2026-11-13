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
/// Flow 3 — the signed-in investor pays an amount in (euros).
/// POST /api/portfolio/payments
/// </summary>
public class PayInEndpoint : IEndpoint<IResult, PayInRequest, string, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PayInRequest request, ClaimsPrincipal user, IPortfolioService service) =>
                await HandleAsync(request, user.BuyerId(), service))
            .Produces<PaymentCreatedResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(PayInRequest request, string buyerId, IPortfolioService service)
    {
        var view = await service.PayInAsync(buyerId, PortfolioEndpointHelpers.Money(request.Amount), default);
        return Results.Created("api/portfolio/payments", new PaymentCreatedResponse(view.PaymentId, view.Status));
    }
}
