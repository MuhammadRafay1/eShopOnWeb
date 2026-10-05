using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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

/// <summary>The signed-in shopper's investments, newest first.</summary>
public class GetInvestmentsEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetInvestmentsEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/investments",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService service) => await HandleAsync(service))
            .Produces<IReadOnlyList<InvestmentItem>>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService service)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var buyerId = httpContext.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();

        var investments = await service.GetInvestmentsAsync(buyerId, httpContext.RequestAborted);
        var items = investments.Select(i => new InvestmentItem
        {
            InvestmentId = i.InvestmentId,
            Amount = i.Amount,
            Status = InvestingStatusText.ToWire(i.Status)
        }).ToList();

        return Results.Ok(items);
    }
}

/// <summary>One investment in the listing.</summary>
public class InvestmentItem
{
    public Guid InvestmentId { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;
}
