using System.Linq;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.Portfolio.PortfolioEndpoints;

/// <summary>POST /api/portfolio/payments — the signed-in investor pays an amount in.</summary>
public class PayInEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PayInRequest request, ClaimsPrincipal user, PaymentService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async shopperId =>
                {
                    var result = await service.PayInAsync(shopperId, request.Amount, ct);
                    return Results.Ok(new PaymentResponse
                    {
                        PaymentId = result.PaymentId.ToString(),
                        Amount = result.Amount,
                        Status = result.Status,
                    });
                }))
            .Produces<PaymentResponse>()
            .WithTags(EndpointSupport.Tag);
    }
}

/// <summary>GET /api/portfolio/payments — the caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user, PaymentService service, CancellationToken ct) =>
                await EndpointSupport.RunAsync(user, async shopperId =>
                {
                    var payments = await service.ListPaymentsAsync(shopperId, ct);
                    return Results.Ok(new PaymentsResponse
                    {
                        Payments = payments.Select(p => new PaymentResponse
                        {
                            PaymentId = p.PaymentId.ToString(),
                            Amount = p.Amount,
                            Status = p.Status,
                        }).ToList(),
                    });
                }))
            .Produces<PaymentsResponse>()
            .WithTags(EndpointSupport.Tag);
    }
}
