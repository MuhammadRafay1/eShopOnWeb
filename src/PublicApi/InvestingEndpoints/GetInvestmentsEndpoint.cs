using System;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

public class InvestmentResponse
{
    public int InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = "pending";
}

/// <summary>Lists the caller's investments, newest first.</summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, InvestingQueryRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService investing, HttpContext httpContext) =>
            {
                var request = new InvestingQueryRequest { BuyerId = httpContext.User.Identity?.Name ?? string.Empty };
                return await HandleAsync(request, investing);
            })
            .Produces<InvestmentResponse[]>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(InvestingQueryRequest request, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }

        var investments = await investing.GetInvestmentsAsync(request.BuyerId, default);
        var response = investments
            .Select(i => new InvestmentResponse
            {
                InvestmentId = i.Id,
                Amount = i.Amount,
                Status = InvestingStatusText.For(i.Status)
            })
            .ToArray();

        return Results.Ok(response);
    }
}
