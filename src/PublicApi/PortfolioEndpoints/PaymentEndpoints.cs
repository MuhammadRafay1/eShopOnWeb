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

/// <summary>The signed-in investor pays an amount in, to be invested across their model's funds.</summary>
public class CreatePaymentEndpoint : IEndpoint<IResult, CreatePaymentRequest, IManagedPortfolioService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreatePaymentEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentRequest request, IManagedPortfolioService service) => await HandleAsync(request, service))
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CreatePaymentRequest request, IManagedPortfolioService service)
    {
        var shopperId = PortfolioMapping.ShopperId(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }
        if (request is null || request.Amount <= 0)
        {
            return Results.BadRequest("amount must be a positive number of euros.");
        }

        var payment = await service.PayInAsync(shopperId, request.Amount);
        return Results.Created("api/portfolio/payments", payment.ToResponse());
    }
}

/// <summary>Lists the caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint<IResult, IManagedPortfolioService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ListPaymentsEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IManagedPortfolioService service) => await HandleAsync(service))
            .Produces<IReadOnlyList<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(IManagedPortfolioService service)
    {
        var shopperId = PortfolioMapping.ShopperId(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(shopperId))
        {
            return Results.Unauthorized();
        }

        var payments = await service.ListPaymentsAsync(shopperId);
        return Results.Ok(payments.Select(p => p.ToResponse()).ToList());
    }
}
