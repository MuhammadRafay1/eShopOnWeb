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

/// <summary>Reports the signed-in shopper's set-aside and invested totals.</summary>
public class BalanceGetEndpoint : IEndpoint<IResult, HttpContext>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (HttpContext context, System.Security.Claims.ClaimsPrincipal user) => await HandleAsync(context))
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(HttpContext context)
    {
        var shopperId = context.User.Identity?.Name;
        if (string.IsNullOrEmpty(shopperId)) return Results.Unauthorized();

        var investingService = context.RequestServices.GetRequiredService<IInvestingService>();
        var investor = await investingService.GetEnrolmentAsync(shopperId, CancellationToken.None);

        return Results.Ok(new BalanceResponse
        {
            PendingAmount = investor?.PendingAmount ?? 0m,
            InvestedAmount = investor?.InvestedAmount ?? 0m,
        });
    }
}

public class BalanceResponse : BaseResponse
{
    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal PendingAmount { get; set; }

    [JsonConverter(typeof(MoneyJsonConverter))]
    public decimal InvestedAmount { get; set; }
}
