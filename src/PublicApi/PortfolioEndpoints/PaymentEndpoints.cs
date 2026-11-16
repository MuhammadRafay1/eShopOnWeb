using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Portfolio.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 3 — the signed-in investor pays an amount in (euros).</summary>
public class CreatePaymentEndpoint : IEndpoint<IResult, CreatePaymentRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentRequest request, IPaymentService service, HttpContext http, CancellationToken ct) =>
            {
                request.CallerId = http.User.ShopperId();
                request.Cancellation = ct;
                return await HandleAsync(request, service);
            })
            .Produces<PaymentResponse>(StatusCodes.Status201Created)
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(CreatePaymentRequest request, IPaymentService service) =>
        EndpointSupport.Guarded(async () =>
        {
            var result = await service.PayInAsync(request.CallerId, request.Amount, request.Cancellation);
            return Results.Created("api/portfolio/payments", ToResponse(result));
        });

    internal static PaymentResponse ToResponse(PaymentResult result) => new()
    {
        PaymentId = result.PaymentId,
        Amount = result.Amount,
        Status = StatusText.For(result.State),
    };
}

/// <summary>Flow 3 — the caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint<IResult, ListPaymentsRequest, IPaymentService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IPaymentService service, HttpContext http, CancellationToken ct) =>
            {
                return await HandleAsync(new ListPaymentsRequest { CallerId = http.User.ShopperId(), Cancellation = ct }, service);
            })
            .Produces<List<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }

    public Task<IResult> HandleAsync(ListPaymentsRequest request, IPaymentService service) =>
        EndpointSupport.Guarded(async () =>
        {
            var payments = await service.ListAsync(request.CallerId, request.Cancellation);
            return Results.Ok(payments.Select(CreatePaymentEndpoint.ToResponse).ToList());
        });
}
