using System;
using System.Collections.Generic;
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

public class InvestmentDto
{
    public Guid InvestmentId { get; set; }

    /// <summary>The amount invested, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    /// <summary><c>pending</c>, <c>settled</c> or <c>failed</c>.</summary>
    public string Status { get; set; } = "pending";
}

public class InvestmentsResponse
{
    public List<InvestmentDto> Investments { get; set; } = new();
}

/// <summary>Lists the signed-in shopper's investments, newest first.</summary>
public class InvestmentsEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public InvestmentsEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IInvestingService investing) => await HandleAsync(investing))
            .Produces<InvestmentsResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService investing)
    {
        var buyerId = CallerIdentity.GetBuyerId(_httpContextAccessor);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var investments = await investing.GetInvestmentsAsync(buyerId);
        return Results.Ok(new InvestmentsResponse
        {
            Investments = investments.Select(i => new InvestmentDto
            {
                InvestmentId = i.PublicId,
                Amount = i.Amount,
                Status = InvestingStatus.ToApi(i.Status),
            }).ToList(),
        });
    }
}
