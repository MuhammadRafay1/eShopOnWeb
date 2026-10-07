using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The signed-in shopper's investments, newest first (GET api/investing/investments).</summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, string, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, IInvestingService investingService) =>
            {
                var buyerId = user.FindFirstValue(ClaimTypes.Name);
                return string.IsNullOrEmpty(buyerId) ? Results.Unauthorized() : await HandleAsync(buyerId, investingService);
            })
            .Produces<InvestmentsResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, IInvestingService investingService)
    {
        var investments = await investingService.GetInvestmentsAsync(buyerId);

        var response = new InvestmentsResponse
        {
            Investments = investments
                .Select(i => new InvestmentItem
                {
                    InvestmentId = i.InvestmentId,
                    Amount = decimal.Round(i.Amount, 2),
                    Status = InvestingStatusText.Wire(i.Status),
                })
                .ToList(),
        };
        return Results.Ok(response);
    }
}

public class InvestmentsResponse : BaseResponse
{
    public List<InvestmentItem> Investments { get; set; } = new();
}

public class InvestmentItem
{
    public Guid InvestmentId { get; set; }

    /// <summary>The euro amount invested.</summary>
    public decimal Amount { get; set; }

    /// <summary>"pending" until the outcome at Upvest is known, then "settled" or "failed".</summary>
    public string Status { get; set; } = "pending";
}
