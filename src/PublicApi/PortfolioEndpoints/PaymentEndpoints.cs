using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Portfolio;
using MinimalApi.Endpoint;
using static Microsoft.eShopWeb.PublicApi.PortfolioEndpoints.PortfolioEndpointSupport;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>POST /api/portfolio/payments — the signed-in investor pays an amount in.</summary>
public class PayInEndpoint : IEndpoint<IResult, PayInRequest, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public PayInEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PayInRequest request, IUpvestPortfolioService service) => await HandleAsync(request, service))
            .Produces<PaymentCreatedResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(PayInRequest request, IUpvestPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var shopper = ResolveShopper(ctx.User);
        if (shopper is null) return Results.Unauthorized();
        if (request is null || request.Amount <= 0m)
        {
            return Results.BadRequest(new { error = "A positive amount (in euros) is required." });
        }

        try
        {
            var result = await service.PayInAsync(shopper, decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero), ctx.RequestAborted);
            return Results.Ok(new PaymentCreatedResponse
            {
                PaymentId = result.PaymentId,
                Status = PortfolioStatusText.For(result.Status)
            });
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }
}

/// <summary>GET /api/portfolio/payments — the caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint<IResult, IUpvestPortfolioService>
{
    private readonly IHttpContextAccessor _http;
    public ListPaymentsEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IUpvestPortfolioService service) => await HandleAsync(service))
            .Produces<List<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IUpvestPortfolioService service)
    {
        var ctx = _http.HttpContext!;
        var shopper = ResolveShopper(ctx.User);
        if (shopper is null) return Results.Unauthorized();

        try
        {
            var payments = await service.ListPaymentsAsync(shopper, ctx.RequestAborted);
            return Results.Ok(payments.Select(p => new PaymentResponse
            {
                PaymentId = p.PaymentId,
                Amount = decimal.Round(p.Amount, 2, MidpointRounding.AwayFromZero),
                Status = PortfolioStatusText.For(p.Status)
            }).ToList());
        }
        catch (Exception ex)
        {
            return TryMapError(ex) ?? throw ex;
        }
    }
}
