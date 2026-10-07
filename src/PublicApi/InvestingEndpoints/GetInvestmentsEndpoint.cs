using System;
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

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>GET /api/investing/investments — the caller's investments, newest first.</summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, ShopperScopedRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService service, HttpContext http) =>
            {
                var shopperId = InvestingShared.ShopperId(http.User);
                if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();
                return await HandleAsync(new ShopperScopedRequest(shopperId), service);
            })
            .Produces<IEnumerable<InvestmentResponse>>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(ShopperScopedRequest request, IInvestingService service)
    {
        var investments = await service.GetInvestmentsAsync(request.ShopperId);
        var response = investments
            .Select(i => new InvestmentResponse(i.InvestmentId, i.Amount, i.Status.ToText()))
            .ToList();
        return Results.Ok(response);
    }
}

public record InvestmentResponse(Guid InvestmentId, decimal Amount, string Status);
