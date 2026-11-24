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
using static Microsoft.eShopWeb.PublicApi.PortfolioEndpoints.PortfolioEndpointHelpers;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 3 — the signed-in investor pays an amount in (credited and invested at Upvest).</summary>
public class CreatePaymentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (CreatePaymentRequest request, ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Unauthenticated();
                }

                var result = await service.PayAsync(buyerId, request.Amount, ct);
                return ToHttpResult(result, v => new CreatePaymentResponse(v.PaymentId, v.Status), StatusCodes.Status201Created);
            })
            .Produces<CreatePaymentResponse>(StatusCodes.Status201Created)
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>Flow 3 — the caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = GetBuyerId(user);
                if (string.IsNullOrEmpty(buyerId))
                {
                    return Unauthenticated();
                }

                var result = await service.GetPaymentsAsync(buyerId, ct);
                return ToHttpResult(result, payments => (object)payments
                    .Select(p => new PaymentResponse(p.PaymentId, Money(p.Amount), p.Status))
                    .ToList());
            })
            .Produces<System.Collections.Generic.List<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}
