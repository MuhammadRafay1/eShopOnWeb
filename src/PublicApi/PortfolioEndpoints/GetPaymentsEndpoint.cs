using System;
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

/// <summary>
/// GET /api/portfolio/payments — the caller's payments, newest first.
/// </summary>
public class GetPaymentsEndpoint : IEndpoint<IResult, CallerRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio/payments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = user.Identity?.Name;
                if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();
                var request = new CallerRequest();
                request.Bind(buyerId, ct);
                return await HandleAsync(request, service);
            })
            .Produces<List<PaymentListItem>>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CallerRequest request, IPortfolioService service)
    {
        var payments = await service.GetPaymentsAsync(request.BuyerId, request.Cancellation);
        var response = payments.Select(p => new PaymentListItem
        {
            PaymentId = p.PaymentId,
            Amount = decimal.Round(p.Amount, 2, MidpointRounding.AwayFromZero),
            Status = StatusText.For(p.Status),
        }).ToList();
        return Results.Ok(response);
    }
}
