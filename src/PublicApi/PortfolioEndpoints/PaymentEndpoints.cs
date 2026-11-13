using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>POST /api/portfolio/payments — the signed-in investor pays an amount in (euros).</summary>
public class PayInEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PayInRequest request, HttpContext http, IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var shopper = PortfolioUser.Name(http.User);
                var result = await svc.PayInAsync(shopper, request.Amount, ct);
                return Results.Ok(new PaymentResponse(result.PaymentId, Money(result.Amount), result.Status));
            })
            .Produces<PaymentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    internal static decimal Money(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>GET /api/portfolio/payments — the caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, IInvestorPortfolioService svc, CancellationToken ct) =>
            {
                var shopper = PortfolioUser.Name(http.User);
                var payments = await svc.ListPaymentsAsync(shopper, ct);
                return Results.Ok(payments
                    .Select(p => new PaymentResponse(p.PaymentId, PayInEndpoint.Money(p.Amount), p.Status))
                    .ToList());
            })
            .Produces<List<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}
