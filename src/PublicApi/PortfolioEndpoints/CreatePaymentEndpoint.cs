using System;
using System.Security.Claims;
using System.Threading;
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

/// <summary>
/// POST /api/portfolio/payments — the signed-in investor pays an amount (euros) in.
/// </summary>
public class CreatePaymentEndpoint : IEndpoint<IResult, CreatePaymentRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreatePaymentRequest request, ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = user.Identity?.Name;
                if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();
                request.Bind(buyerId, ct);
                return await HandleAsync(request, service);
            })
            .Produces<PaymentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CreatePaymentRequest request, IPortfolioService service)
    {
        if (request.Amount <= 0m)
        {
            throw new PortfolioValidationException("amount must be greater than zero.");
        }

        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var payment = await service.PayInAsync(request.BuyerId, amount, request.Cancellation);

        return Results.Ok(new PaymentResponse
        {
            PaymentId = payment.PaymentId,
            Status = StatusText.For(payment.Status),
        });
    }
}
