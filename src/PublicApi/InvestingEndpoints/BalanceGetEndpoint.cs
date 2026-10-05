using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.InvestingEndpoints;

/// <summary>What the caller has set aside and the total invested so far (Flow 5).</summary>
public class BalanceGetEndpoint : IEndpoint<IResult, ClaimsPrincipal>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public BalanceGetEndpoint(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/investing/balance",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
            async (ClaimsPrincipal user) => await HandleAsync(user))
            .Produces<BalanceResponse>()
            .WithTags("InvestingEndpoints");
    }

    public async Task<IResult> HandleAsync(ClaimsPrincipal user)
    {
        var shopperId = user.ShopperId();
        if (string.IsNullOrEmpty(shopperId))
            return Results.Unauthorized();

        using var scope = _scopeFactory.CreateScope();
        var investing = scope.ServiceProvider.GetRequiredService<InvestingService>();

        var (pendingCents, investedCents) = await investing.GetBalanceAsync(shopperId, default);
        return Results.Ok(new BalanceResponse
        {
            PendingAmount = InvestingMappings.ToEuros(pendingCents),
            InvestedAmount = InvestingMappings.ToEuros(investedCents),
        });
    }
}
