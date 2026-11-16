using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

public sealed class PayInRequestEnvelope : ShopperRequest
{
    public PayInRequest Body { get; set; } = default!;
}

/// <summary>
/// POST /api/portfolio/payments — the signed-in investor pays an amount in (euros).
/// </summary>
public sealed class PayInEndpoint : IEndpoint<IResult, PayInRequestEnvelope, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (PayInRequest body, HttpContext http, IPortfolioService service) =>
                await HandleAsync(new PayInRequestEnvelope
                {
                    Body = body,
                    ShopperId = PortfolioEndpointHelpers.ShopperId(http),
                    CancellationToken = http.RequestAborted
                }, service))
            .Produces<PaymentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(PayInRequestEnvelope request, IPortfolioService service)
    {
        if (request.Body is null)
            throw PortfolioException.BadRequest("A request body is required.");

        var result = await service.PayInAsync(request.ShopperId, request.Body.Amount, request.CancellationToken);
        return Results.Ok(result.ToResponse());
    }
}

public sealed class ListPaymentsRequest : ShopperRequest { }

/// <summary>
/// GET /api/portfolio/payments — the caller's payments, newest first.
/// </summary>
public sealed class ListPaymentsEndpoint : IEndpoint<IResult, ListPaymentsRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (HttpContext http, IPortfolioService service) =>
                await HandleAsync(new ListPaymentsRequest
                {
                    ShopperId = PortfolioEndpointHelpers.ShopperId(http),
                    CancellationToken = http.RequestAborted
                }, service))
            .Produces<PaymentListResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(ListPaymentsRequest request, IPortfolioService service)
    {
        var payments = await service.GetPaymentsAsync(request.ShopperId, request.CancellationToken);
        return Results.Ok(new PaymentListResponse(payments.Select(p => p.ToResponse()).ToList()));
    }
}
