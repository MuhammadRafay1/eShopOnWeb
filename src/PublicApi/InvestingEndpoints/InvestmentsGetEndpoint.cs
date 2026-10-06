using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>Lists the signed-in shopper's investments, newest first.</summary>
public class InvestmentsGetEndpoint : IEndpoint<IResult, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext context, System.Security.Claims.ClaimsPrincipal user) => await HandleAsync(context))
            .Produces<InvestmentsResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext context)
    {
        var shopperId = context.User.Identity?.Name;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var investingService = context.RequestServices.GetRequiredService<IInvestingService>();
        var investments = await investingService.GetInvestmentsAsync(shopperId, CancellationToken.None);

        var response = new InvestmentsResponse
        {
            Investments = investments.Select(i => new InvestmentDto
            {
                InvestmentId = i.PublicId,
                Amount = i.Amount,
                Status = i.Status.ToWire(),
            }).ToList(),
        };
        return Results.Ok(response);
    }
}

public class InvestmentsResponse : BaseResponse
{
    public List<InvestmentDto> Investments { get; set; } = new();
}

public class InvestmentDto
{
    public Guid InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = "pending";
}
