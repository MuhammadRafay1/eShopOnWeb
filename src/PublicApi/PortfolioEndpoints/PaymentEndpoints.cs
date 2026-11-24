using System.Collections.Generic;
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

/// <summary>POST /api/portfolio/payments — the investor pays an amount in (euros).</summary>
public class CreatePaymentEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PayInRequest request, ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var unauthorized = user.RequireUserName(out var userName);
                if (unauthorized != null) return unauthorized;
                if (request.Amount <= 0m)
                    return Results.BadRequest(new { message = "amount must be greater than zero." });

                var result = await service.PayInAsync(userName, request.Amount, ct);
                return Results.Created("api/portfolio/payments",
                    new PaymentCreatedResponse { PaymentId = result.PaymentId, Status = result.Status.ToString().ToLowerInvariant() });
            })
            .Produces<PaymentCreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("PortfolioEndpoints");
    }
}

/// <summary>GET /api/portfolio/payments — the caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IManagedPortfolioService service, CancellationToken ct) =>
            {
                var unauthorized = user.RequireUserName(out var userName);
                if (unauthorized != null) return unauthorized;

                var payments = await service.GetPaymentsAsync(userName, ct);
                return Results.Ok(payments.Select(p => p.ToResponse()).ToList());
            })
            .Produces<List<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}
