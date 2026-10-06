using System;
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

public class BalanceResponse : BaseResponse
{
    public BalanceResponse(Guid correlationId) : base(correlationId) { }
    public BalanceResponse() { }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}

/// <summary>Reports what the caller currently has set aside and the total invested so far.</summary>
public class GetBalanceEndpoint : IEndpoint<IResult, InvestingQueryRequest, IInvestingService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService investing, HttpContext httpContext) =>
            {
                var request = new InvestingQueryRequest { BuyerId = httpContext.User.Identity?.Name ?? string.Empty };
                return await HandleAsync(request, investing);
            })
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(InvestingQueryRequest request, IInvestingService investing)
    {
        if (string.IsNullOrEmpty(request.BuyerId))
        {
            return Results.Unauthorized();
        }

        var summary = await investing.GetBalanceAsync(request.BuyerId, default);
        var response = new BalanceResponse(request.CorrelationId())
        {
            PendingAmount = summary.PendingAmount,
            InvestedAmount = summary.InvestedAmount
        };
        return Results.Ok(response);
    }
}
