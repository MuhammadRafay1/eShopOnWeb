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

/// <summary>What the signed-in shopper currently has set aside and the total invested so far.</summary>
public class GetBalanceEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetBalanceEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (IInvestingService service) => await HandleAsync(service))
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService service)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var buyerId = httpContext.User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(buyerId)) return Results.Unauthorized();

        var balance = await service.GetBalanceAsync(buyerId, httpContext.RequestAborted);
        return Results.Ok(new BalanceResponse
        {
            PendingAmount = balance.PendingAmount,
            InvestedAmount = balance.InvestedAmount
        });
    }
}

/// <summary>The shopper's set-aside and invested amounts (euros, two decimals).</summary>
public class BalanceResponse
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}
