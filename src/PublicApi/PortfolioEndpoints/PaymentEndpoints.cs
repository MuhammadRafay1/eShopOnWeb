using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>POST /api/portfolio/payments — the signed-in investor pays an amount in.</summary>
public class PayInEndpoint : IEndpoint<IResult, PayInRequest, IPortfolioPaymentService>
{
    private readonly IHttpContextAccessor _http;
    public PayInEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (PayInRequest request, IPortfolioPaymentService service) => await HandleAsync(request, service))
            .Produces<PaymentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(PayInRequest request, IPortfolioPaymentService service)
    {
        var ctx = _http.HttpContext!;
        var userName = PortfolioHttp.UserName(ctx);
        if (string.IsNullOrEmpty(userName))
        {
            return Results.Unauthorized();
        }
        if (request is null || request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "A positive amount is required." });
        }

        var view = await service.PayInAsync(userName, PortfolioHttp.Money(request.Amount), ctx.RequestAborted);
        return Results.Ok(new PaymentResponse
        {
            PaymentId = view.PaymentId,
            Amount = PortfolioHttp.Money(view.Amount),
            Status = PortfolioHttp.Status(view.Status)
        });
    }
}

/// <summary>GET /api/portfolio/payments — the caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint<IResult, IPortfolioPaymentService>
{
    private readonly IHttpContextAccessor _http;
    public ListPaymentsEndpoint(IHttpContextAccessor http) => _http = http;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPortfolioPaymentService service) => await HandleAsync(service))
            .Produces<List<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IPortfolioPaymentService service)
    {
        var ctx = _http.HttpContext!;
        var userName = PortfolioHttp.UserName(ctx);
        if (string.IsNullOrEmpty(userName))
        {
            return Results.Unauthorized();
        }

        var payments = await service.ListPaymentsAsync(userName, ctx.RequestAborted);
        var response = payments.Select(p => new PaymentResponse
        {
            PaymentId = p.PaymentId,
            Amount = PortfolioHttp.Money(p.Amount),
            Status = PortfolioHttp.Status(p.Status)
        }).ToList();
        return Results.Ok(response);
    }
}
