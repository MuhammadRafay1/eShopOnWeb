using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.PortfolioAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Interfaces.Upvest;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.PortfolioEndpoints;

/// <summary>Flow 3 — POST /api/portfolio/payments: the signed-in investor pays an amount in (euros).</summary>
public class PayInEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                PayInRequest request,
                ClaimsPrincipal user,
                IPortfolioPaymentService service,
                CancellationToken ct) =>
            {
                var userName = PortfolioEndpointHelpers.GetUserName(user);
                if (string.IsNullOrEmpty(userName)) return Results.Unauthorized();
                if (request.Amount <= 0m)
                    return Results.BadRequest(new { message = "amount must be a positive number of euros." });

                try
                {
                    var payment = await service.PayInAsync(userName, request.Amount, ct);
                    return Results.Ok(ToResponse(payment));
                }
                catch (PaymentRefusedException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (UpvestException ex)
                {
                    return PortfolioEndpointHelpers.ToResult(ex);
                }
            })
            .Produces<PaymentResponse>()
            .WithTags("PortfolioEndpoints");
    }

    internal static PaymentResponse ToResponse(PortfolioPayment p) => new()
    {
        PaymentId = p.Id,
        Amount = p.Amount,
        Status = PortfolioStatusText.Of(p.MapStatus())
    };
}

/// <summary>Flow 3 — GET /api/portfolio/payments: the caller's payments, newest first.</summary>
public class ListPaymentsEndpoint : IEndpoint
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async (
                ClaimsPrincipal user,
                IPortfolioPaymentService service,
                CancellationToken ct) =>
            {
                var userName = PortfolioEndpointHelpers.GetUserName(user);
                if (string.IsNullOrEmpty(userName)) return Results.Unauthorized();

                var payments = await service.ListAsync(userName, ct);
                return Results.Ok(payments.Select(PayInEndpoint.ToResponse).ToList());
            })
            .Produces<System.Collections.Generic.List<PaymentResponse>>()
            .WithTags("PortfolioEndpoints");
    }
}
