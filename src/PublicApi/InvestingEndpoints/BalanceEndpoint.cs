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

public class BalanceResponse
{
    /// <summary>What the shopper has set aside and not yet invested, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    /// <summary>The total amount invested so far, in euros.</summary>
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}

/// <summary>Reports the signed-in shopper's spare-change balance.</summary>
public class BalanceEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BalanceEndpoint(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IInvestingService investing) => await HandleAsync(investing))
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService investing)
    {
        var buyerId = CallerIdentity.GetBuyerId(_httpContextAccessor);
        if (string.IsNullOrEmpty(buyerId))
        {
            return Results.Unauthorized();
        }

        var balance = await investing.GetBalanceAsync(buyerId);
        return Results.Ok(new BalanceResponse
        {
            PendingAmount = balance.PendingAmount,
            InvestedAmount = balance.InvestedAmount,
        });
    }
}
