using System;
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
/// GET /api/portfolio — the caller's portfolio as Upvest currently records it.
/// </summary>
public class GetPortfolioEndpoint : IEndpoint<IResult, CallerRequest, IPortfolioService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/portfolio",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IPortfolioService service, CancellationToken ct) =>
            {
                var buyerId = user.Identity?.Name;
                if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();
                var request = new CallerRequest();
                request.Bind(buyerId, ct);
                return await HandleAsync(request, service);
            })
            .Produces<PortfolioResponse>()
            .WithTags("PortfolioEndpoints");
    }

    public async Task<IResult> HandleAsync(CallerRequest request, IPortfolioService service)
    {
        var portfolio = await service.GetPortfolioAsync(request.BuyerId, request.Cancellation);

        return Results.Ok(new PortfolioResponse
        {
            ModelId = portfolio.ModelId,
            Holdings = portfolio.Holdings.Select(h => new HoldingDto
            {
                InstrumentId = h.InstrumentId,
                Quantity = h.Quantity,
                Value = decimal.Round(h.Value, 2, MidpointRounding.AwayFromZero),
            }).ToList(),
            TotalValue = decimal.Round(portfolio.TotalValue, 2, MidpointRounding.AwayFromZero),
            PaidIn = decimal.Round(portfolio.PaidIn, 2, MidpointRounding.AwayFromZero),
        });
    }
}
