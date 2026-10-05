using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>The caller's set-aside and invested totals (Flow 5).</summary>
public class BalanceEndpoint : IEndpoint<IResult, IInvestingService>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BalanceEndpoint(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (IInvestingService investingService) => await HandleAsync(investingService))
            .Produces<BalanceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(IInvestingService investingService)
    {
        var buyerId = CallerId.Resolve(_httpContextAccessor.HttpContext?.User);
        if (string.IsNullOrEmpty(buyerId))
            return Results.Unauthorized();

        var balance = await investingService.GetBalanceAsync(buyerId, _httpContextAccessor.HttpContext!.RequestAborted);
        return balance is null
            ? Results.NotFound(new { error = "Not enrolled." })
            : Results.Ok(new BalanceResponse
            {
                PendingAmount = MoneyFormat.Euros(balance.PendingCents),
                InvestedAmount = MoneyFormat.Euros(balance.InvestedCents),
            });
    }
}

public class BalanceResponse
{
    public decimal PendingAmount { get; set; }
    public decimal InvestedAmount { get; set; }
}
